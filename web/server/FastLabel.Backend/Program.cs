using FastLabel.Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration, Path.Combine(Directory.GetCurrentDirectory(), "FastLabel.Server.db"));

var app = builder.Build();

app.ConfigurePipeline();

app.Run();