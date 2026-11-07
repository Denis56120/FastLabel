using FastLabel.Client.Components;
using FastLabel.Client.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Ajout du HttpClient pointant vers l'API locale
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5000/") });

builder.Services.AddScoped<AuthService>();
builder.Services.AddTransient<CustomAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => 
    new HttpClient(sp.GetRequiredService<CustomAuthorizationMessageHandler>()) 
    { 
        BaseAddress = new Uri("http://localhost:5000/") 
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();