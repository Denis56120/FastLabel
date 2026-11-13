var envpath = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envpath)) 
{
    Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration, Path.Combine(Directory.GetCurrentDirectory(), "FastLabel.Server.db"));

var app = builder.Build();

app.ConfigurePipeline();

app.Run();