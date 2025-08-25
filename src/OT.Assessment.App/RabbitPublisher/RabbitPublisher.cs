using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.App.Models;
using RabbitMQ.Client;

namespace OT.Assessment.App.RabbitPublisher;

public interface IRabbitPublisher
{
    Task PublishAsync(Models.CasinoWagerMessage message, CancellationToken ct);
}

public sealed class RabbitPublisher : IRabbitPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly RabbitMqOptions _opts;
    private readonly ILogger<RabbitPublisher> _log;

    public RabbitPublisher(IOptions<RabbitMqOptions> opts, ILogger<RabbitPublisher> log)
    {
        _opts = opts.Value;
        _log = log;

        var factory = new ConnectionFactory
        {
            HostName = _opts.HostName,
            Port = _opts.Port,
            UserName = _opts.UserName,
            Password = _opts.Password,
            DispatchConsumersAsync = true,
            // give the broker a bigger window to respond to RPCs (not used for publish here)
            ContinuationTimeout = TimeSpan.FromSeconds(30)
        };

        _connection = factory.CreateConnection();
    }

    public Task PublishAsync(Models.CasinoWagerMessage message, CancellationToken ct)
    {
        // channel-per-publish => thread-safe, no locks, no cross-request contention
        using var ch = _connection.CreateModel();

        ch.QueueDeclare(queue: _opts.QueueName,
                        durable: _opts.Durable,
                        exclusive: false,
                        autoDelete: false);

        var props = ch.CreateBasicProperties();
        props.DeliveryMode = 2; // persistent
        props.ContentType = "application/json";
        props.MessageId = message.WagerId.ToString();

        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        ch.BasicPublish(exchange: "",
                        routingKey: _opts.QueueName,
                        mandatory: false,
                        basicProperties: props,
                        body: body);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _connection?.Close(); } catch { /* ignore */ }
        _connection?.Dispose();
    }
}
