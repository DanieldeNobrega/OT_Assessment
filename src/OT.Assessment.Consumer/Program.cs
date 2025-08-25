using Microsoft.Extensions.Configuration;
using OT.Assessment.Consumer;
using OT.Assessment.Consumer.Models;
using OT.Assessment.Consumer.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
    })
    .ConfigureServices((context, services) =>
    {
        //configure services
        services.Configure<RabbitMqOptions>(context.Configuration.GetSection("RabbitMq"));

        var cs = context.Configuration.GetConnectionString("DatabaseConnection")
                 ?? throw new InvalidOperationException("Missing ConnectionStrings:DatabaseConnection");
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(cs));

        services.AddSingleton<IWagerWriter, WagerWriter>();
        services.AddHostedService<WagerConsumerService>();

        services.AddLogging();
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started {time:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

await host.RunAsync();

logger.LogInformation("Application ended {time:yyyy-MM-dd HH:mm:ss}", DateTime.Now);