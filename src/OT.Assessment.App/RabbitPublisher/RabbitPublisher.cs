using Microsoft.Extensions.Options;
using OT.Assessment.App.Models;
using RabbitMQ.Client;
using System.Text;

namespace OT.Assessment.App.RabbitPublisher
{
    public interface IRabbitPublisher
    {
        Task PublishAsync(CasinoWagerMessage message, CancellationToken ct);
    }

    public sealed class RabbitPublisher : IRabbitPublisher, IDisposable
    {
        private readonly RabbitMqOptions _opts;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitPublisher(IOptions<RabbitMqOptions> options)
        {
            _opts = options.Value;
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
            _channel.QueueDeclare(queue: _opts.QueueName, durable: _opts.Durable, exclusive: false, autoDelete: false, arguments: null);
            _channel.ConfirmSelect();
        }

        public Task PublishAsync(CasinoWagerMessage message, CancellationToken ct)
        {
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            var props = _channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent

            _channel.BasicPublish(exchange: "", routingKey: _opts.QueueName, basicProperties: props, body: body);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }
        }
    }
}
