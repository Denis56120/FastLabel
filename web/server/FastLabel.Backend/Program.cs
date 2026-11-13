var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEnvironmentConfiguration();
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.ConfigurePipeline();

app.Run();