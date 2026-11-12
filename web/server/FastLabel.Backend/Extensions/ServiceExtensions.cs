using System.Text;
using FastLabel.Backend.Data;
using FastLabel.Backend.Models;
using FastLabel.Backend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FastLabel.Backend.Extensions;

public static class ServiceExtensions
{

    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration, string dbPath)
    {
        var clientUrl = configuration["ClientUrl"] ?? "http://localhost:5000";
        
        services.AddCors(options =>
        {
            options.AddPolicy("AllowBlazorClient", policy =>
            {
                policy.WithOrigins(clientUrl)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddControllers();
        services.AddScoped<TokenService>();
        services.AddScoped<IEmailService, EmailService>();

        services.AddSingleton<PrintBrokerService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
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

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        return services;
    }
}