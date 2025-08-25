using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using OT.Assessment.Consumer.Models;

public sealed class WagerConsumerService : BackgroundService
{
    private readonly ILogger<WagerConsumerService> _log;
    private readonly IWagerWriter _writer;
    private readonly RabbitMqOptions _opts;

    private IConnection? _conn;
    private IModel? _ch;

    private readonly Channel<CasinoWagerMessage> _buffer =
        Channel.CreateBounded<CasinoWagerMessage>(new BoundedChannelOptions(50000)
        { SingleWriter = false, SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private const int BatchSize = 1000;
    private static readonly TimeSpan FlushEvery = TimeSpan.FromMilliseconds(200);

    public WagerConsumerService(ILogger<WagerConsumerService> log, IWagerWriter writer, IOptions<RabbitMqOptions> opts)
    { _log = log; _writer = writer; _opts = opts.Value; }

    public override Task StartAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.HostName,
            Port = _opts.Port,
            UserName = _opts.UserName,
            Password = _opts.Password,
            DispatchConsumersAsync = true
        };
        _conn = factory.CreateConnection();
        _ch = _conn.CreateModel();
        _ch.QueueDeclare(_opts.QueueName, _opts.Durable, false, false);
        _ch.BasicQos(0, _opts.PrefetchCount, false);
        _log.LogInformation("Consumer connected to Rabbit. Queue={Queue}", _opts.QueueName);
        return base.StartAsync(ct);
    }

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        if (_ch is null) throw new InvalidOperationException("Channel not initialized.");

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var msg = JsonSerializer.Deserialize<CasinoWagerMessage>(ea.Body.Span);
                if (msg is not null)
                    await _buffer.Writer.WriteAsync(msg, ct);

                _ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to process message {Tag}", ea.DeliveryTag);
                _ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };
        _ch.BasicConsume(_opts.QueueName, autoAck: false, consumer);

        // writer loop
        _ = Task.Run(() => BatchWriterLoop(ct), ct);
        return Task.CompletedTask;
    }

    private async Task BatchWriterLoop(CancellationToken ct)
    {
        var timer = new PeriodicTimer(FlushEvery);
        var batch = new List<CasinoWagerMessage>(BatchSize);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                while (batch.Count < BatchSize && _buffer.Reader.TryRead(out var m))
                    batch.Add(m);

                if (batch.Count == 0)
                {
                    await timer.WaitForNextTickAsync(ct);
                    continue;
                }

                await _writer.InsertBulkAsync(batch, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Bulk insert failed; retrying next tick");
                await Task.Delay(100, ct);
            }
            finally
            {
                batch.Clear();
            }
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
