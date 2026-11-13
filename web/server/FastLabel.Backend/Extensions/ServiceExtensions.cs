namespace FastLabel.Backend.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddEnvironmentConfiguration(this IServiceCollection services)
    {
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }

        return services;
    }
    public static IServiceCollection AddAppServices(this IServiceCollection services, IConfiguration configuration)
    {
        var clientUrl = configuration["Url:ClientUrl"] ?? "https://fastlabel.com";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                       ?? $"Host=localhost;Port=5432;Database={configuration["DB_NAME"] ?? string.Empty};Username={configuration["DB_USER"] ?? string.Empty};Password={configuration["DB_PASSWORD"]}";

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

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

        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errorMessage = context.ModelState
                        .Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .FirstOrDefault() ?? string.Empty;

                    var response = new ErrorResponseDto { Message = errorMessage };
                    return new BadRequestObjectResult(response);
                };
            });
        services.AddScoped<IEmailService, EmailService>();

        services.AddSingleton<PrintBrokerService>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "authentification";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Enum.TryParse<CookieSecurePolicy>(configuration["CookieSettings:SecurePolicy"], out var securePolicy) ? securePolicy : CookieSecurePolicy.SameAsRequest;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}