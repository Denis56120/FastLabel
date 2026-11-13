var builder = WebAssemblyHostBuilder.CreateDefault(args);
var serverAddress = builder.Configuration["Url:ServerUrl"] ?? "https://fastlabel.com";

builder.RootComponents.Add<App>("#app");

builder.Services.AddAuthorizationCore();

builder.Services.AddTransient<CustomAuthorizationMessageHandler>();

builder.Services.AddHttpClient("PublicClient", client =>
{
    client.BaseAddress = new Uri(serverAddress);
});

builder.Services.AddHttpClient("AuthClient", client =>
{
    client.BaseAddress = new Uri(serverAddress);
})
.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

builder.Services.AddSingleton<CookieService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<TranslationService>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddScoped(sp => 
{
    var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = clientFactory.CreateClient("AuthClient");
    return new ApiClient(httpClient);
});

var host = builder.Build();

await host.RunAsync();