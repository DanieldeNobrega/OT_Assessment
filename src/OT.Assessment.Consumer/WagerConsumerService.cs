using Microsoft.Extensions.Options;
using OT.Assessment.Consumer.Models;
using OT.Assessment.Consumer.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OT.Assessment.Consumer;

public sealed class WagerConsumerService : BackgroundService
{
    private readonly ILogger<WagerConsumerService> _logger;
    private readonly IWagerWriter _writer;
    private readonly RabbitMqOptions _opts;
    private IConnection? _connection;
    private IModel? _channel;

    public WagerConsumerService(ILogger<WagerConsumerService> logger, IWagerWriter writer, IOptions<RabbitMqOptions> options)
    {
        _logger = logger;
        _writer = writer;
        _opts = options.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.HostName,
            Port = _opts.Port,
            UserName = _opts.UserName,
            Password = _opts.Password,
            DispatchConsumersAsync = true
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(_opts.QueueName, durable: _opts.Durable, exclusive: false, autoDelete: false, arguments: null);
        _channel.BasicQos(0, _opts.PrefetchCount, false);
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null) throw new InvalidOperationException("Channel not initialized.");
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (ch, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var msg = JsonSerializer.Deserialize<CasinoWagerMessage>(json);
                if (msg != null)
                {
                    await _writer.InsertAsync(msg, stoppingToken);
                }
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message");
                // Nack & requeue=false to prevent poison message loops (could be DLQ in prod)
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: _opts.QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try { _channel?.Close(); } catch { }
        try { _connection?.Close(); } catch { }
        return base.StopAsync(cancellationToken);
    }
}
