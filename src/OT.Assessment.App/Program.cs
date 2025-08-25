using System.Reflection;
using OT.Assessment.App.Messaging;     // ✅ buffered publisher types
using OT.Assessment.App.Models;
using OT.Assessment.App.Repository;    // IDbConnectionFactory, SqlServerConnectionFactory, PlayerReadRepository

var builder = WebApplication.CreateBuilder(args);

// ThreadPool headroom under load tests (optional but helpful)
ThreadPool.SetMinThreads(200, 200);

// Bind Rabbit options from appsettings.json to the *Messaging* RabbitMqOptions
builder.Services.Configure<OT.Assessment.App.Models.RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

// DB for GET endpoints
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlServerConnectionFactory(builder.Configuration.GetConnectionString("DatabaseConnection")!));
builder.Services.AddScoped<IPlayerReadRepository, PlayerReadRepository>();

// Controllers + JSON
builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNamingPolicy = null;
    opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// 🟢 Buffered publisher: queue + background service
builder.Services.AddSingleton<IPublishQueue, PublishQueue>();
builder.Services.AddHostedService<BufferedPublisherService>();

// ❌ REMOVE these two lines if they exist anywhere:
// builder.Services.AddSingleton<IRabbitPublisher, RabbitPublisher>();
// using OT.Assessment.App.RabbitPublisher;

// Kestrel & URLs
builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 10 * 1024 * 1024; });
builder.WebHost.UseUrls("http://localhost:5120", "https://localhost:7120");

var app = builder.Build();

// Swagger in Dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts =>
    {
        opts.EnableTryItOutByDefault();
        opts.DocumentTitle = "OT Assessment App";
        opts.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();
// app.UseAuthorization(); // keep if you actually use auth

app.MapControllers();
app.Run();
