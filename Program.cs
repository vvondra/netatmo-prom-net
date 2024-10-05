using dotenv.net;
using Prometheus;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<NetatmoService>();
builder.Services.AddHostedService<NetatmoCollector>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMetricServer();

app.Run();