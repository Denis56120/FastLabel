using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using FastLabel.Client;
using FastLabel.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var serverAddress = builder.Configuration["ServerUrl"] ?? "http://localhost:5001";

// 1. Authentification et Autorisation Blazor WASM
builder.Services.AddAuthorizationCore();

// 2. Configuration des clients HTTP nommés
builder.Services.AddTransient<CustomAuthorizationMessageHandler>();

// Client public (pour login, register, etc.)
builder.Services.AddHttpClient("PublicClient", client =>
{
    client.BaseAddress = new Uri(serverAddress);
});

// Client sécurisé (pour les appels authentifiés)
builder.Services.AddHttpClient("AuthClient", client =>
{
    client.BaseAddress = new Uri(serverAddress);
})
.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// 3. Services métiers
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TranslationService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<CookieService>();
builder.Services.AddScoped<TokenContainer>();

await builder.Build().RunAsync();