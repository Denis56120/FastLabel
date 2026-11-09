using FastLabel.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Enregistrement des services
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

// 2. Configuration du pipeline HTTP
app.ConfigurePipeline();

app.Run();