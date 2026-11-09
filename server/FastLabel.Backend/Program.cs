using FastLabel.Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

var baseAddress = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:6000";

builder.Services.AddAppServices(builder.Configuration, Path.Combine(Directory.GetCurrentDirectory(), "FastLabel.Server.db"));

var app = builder.Build();

app.ConfigurePipeline();

app.Run(baseAddress);