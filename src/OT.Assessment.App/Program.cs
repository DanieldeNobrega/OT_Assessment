using OT.Assessment.App.Models;
using OT.Assessment.App.RabbitPublisher;
using OT.Assessment.App.Repository;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlServerConnectionFactory(builder.Configuration.GetConnectionString("DatabaseConnection")!));

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckl
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddSingleton<IRabbitPublisher, RabbitPublisher>();
builder.Services.AddScoped<IPlayerReadRepository, PlayerReadRepository>();

builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; });

builder.WebHost.UseUrls("http://localhost:5120", "https://localhost:7120");

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.UseAuthorization();

app.MapControllers();

app.Run();
