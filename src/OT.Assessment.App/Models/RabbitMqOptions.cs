namespace OT.Assessment.App.Models
{
    public sealed class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string QueueName { get; set; } = "casino-wagers";
        public bool Durable { get; set; } = true;
    }
}
