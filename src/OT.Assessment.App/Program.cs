using System.Reflection;
using OT.Assessment.App.Messaging;     
using OT.Assessment.App.Models;
using OT.Assessment.App.Repository;    

var builder = WebApplication.CreateBuilder(args);

ThreadPool.SetMinThreads(200, 200);

builder.Services.Configure<OT.Assessment.App.Models.RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlServerConnectionFactory(builder.Configuration.GetConnectionString("DatabaseConnection")!));
builder.Services.AddScoped<IPlayerReadRepository, PlayerReadRepository>();

builder.Services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNamingPolicy = null;
    opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddSingleton<IPublishQueue, PublishQueue>();
builder.Services.AddHostedService<BufferedPublisherService>();

builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 10 * 1024 * 1024; });
builder.WebHost.UseUrls("http://localhost:5120", "https://localhost:7120");

var app = builder.Build();

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
// app.UseAuthorization();

app.MapControllers();
app.Run();
