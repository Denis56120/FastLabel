var builder = WebApplication.CreateBuilder(args);

Stripe.StripeConfiguration.ApiKey = builder.Configuration["STRIPE_SECRET_KEY"];

builder.Services.AddEnvironmentConfiguration();
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddAppServices(builder.Configuration);

var app = builder.Build();

app.ConfigurePipeline();

app.Run();