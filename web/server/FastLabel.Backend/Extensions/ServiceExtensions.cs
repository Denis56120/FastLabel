using System.Text;
using FastLabel.Backend.Data;
using FastLabel.Backend.Services;
using FastLabel.Client.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FastLabel.Backend.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration, string dbPath)
    {
        // 1. CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowBlazorClient", policy =>
            {
                policy.WithOrigins()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddControllers();
        services.AddScoped<TokenService>();

        // 2. État partagé (WebSockets / Jobs)
        services.AddSingleton<PrintBrokerService>();

        // 3. Authentification mixte : Cookies pour l'UI Blazor / JWT pour l'API
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
        })
        .AddJwtBearer(options =>
        {
            var secretKey = configuration["Jwt:SecretKey"]
                            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = false,
                ValidateAudience = false
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

        // 4. Base de données SQLite
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        return services;
    }
}