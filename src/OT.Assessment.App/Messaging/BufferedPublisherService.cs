using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using OT.Assessment.App.Models;

namespace OT.Assessment.App.Messaging;

public sealed class BufferedPublisherService : BackgroundService
{
    private readonly ILogger<BufferedPublisherService> _log;
    private readonly IPublishQueue _queue;
    private readonly RabbitMqOptions _opts;

    private IConnection? _conn;
    private IModel? _ch;

    private const int BatchSize = 500;                   // tuneable
    private static readonly TimeSpan FlushEvery = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(15);

    public BufferedPublisherService(
        ILogger<BufferedPublisherService> log,
        IPublishQueue queue,
        IOptions<RabbitMqOptions> opts)
    {
        _log = log;
        _queue = queue;
        _opts = opts.Value;
    }

    public override Task StartAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.HostName,
            Port = _opts.Port,
            UserName = _opts.UserName,
            Password = _opts.Password,
            DispatchConsumersAsync = true,
            ContinuationTimeout = TimeSpan.FromSeconds(30)
        };
        _conn = factory.CreateConnection();
        _ch = _conn.CreateModel();

        _ch.QueueDeclare(_opts.QueueName, durable: _opts.Durable, exclusive: false, autoDelete: false);
        _ch.ConfirmSelect(); // confirms, but per-batch (not per-request)

        _log.LogInformation("BufferedPublisher started. Queue={Queue}", _opts.QueueName);
        return base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (_ch is null) throw new InvalidOperationException("Channel not initialized.");
        var timer = new PeriodicTimer(FlushEvery);
        var batch = new List<CasinoWagerMessage>(BatchSize);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Try drain quickly
                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var m))
                    batch.Add(m);

                // If nothing, wait a tick or until something arrives
                if (batch.Count == 0)
                {
                    var read = await _queue.Reader.WaitToReadAsync(ct);
                    if (!read) continue;
                    continue; // loop again to TryRead
                }

                // publish the batch
                foreach (var msg in batch)
                {
                    var props = _ch.CreateBasicProperties();
                    props.DeliveryMode = 2; // persistent
                    props.ContentType = "application/json";
                    props.MessageId = msg.WagerId.ToString();

                    var body = JsonSerializer.SerializeToUtf8Bytes(msg);
                    _ch.BasicPublish(exchange: "",
                                     routingKey: _opts.QueueName,
                                     mandatory: false,
                                     basicProperties: props,
                                     body: body);
                }

                if (!_ch.WaitForConfirms(ConfirmTimeout))
                    _log.LogWarning("Publisher confirms timed out for batch of {Count}", batch.Count);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to publish batch; messages may be retried next cycle");
                // optional: backoff
                await Task.Delay(200, ct);
            }
            finally
            {
                batch.Clear();
            }

            // small pacing to avoid tight loop
            await timer.WaitForNextTickAsync(ct);
        }
    }

    public override Task StopAsync(CancellationToken ct)
    {
        try { _ch?.Close(); } catch { }
        try { _conn?.Close(); } catch { }
        _ch?.Dispose();
        _conn?.Dispose();
        return base.StopAsync(ct);
    }
}
