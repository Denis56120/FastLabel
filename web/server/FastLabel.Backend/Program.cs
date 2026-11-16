var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEnvironmentConfiguration();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

await app.ConfigurePipelineAsync();

app.Run();