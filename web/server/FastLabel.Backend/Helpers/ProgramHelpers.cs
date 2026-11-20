namespace FastLabel.Backend.Helpers;

public static class ProgramHelpers
{
    public static void AddEnvironmentConfiguration(WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.dev");

            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }
        }

        EnvironmentVariablesExtensions.AddEnvironmentVariables(builder.Configuration);
    }

    public static void AddAppServices(WebApplicationBuilder builder)
    {
        IServiceCollection services = builder.Services;
        ConfigurationManager configuration = builder.Configuration;

        StripeService.SetApiKey(configuration);

        var clientUrl = ConfigurationHelpers.GetClientUrl(configuration);
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                       ?? $"Host=localhost;Port=5432;Database={ConfigurationHelpers.GetDBName(configuration)};Username={ConfigurationHelpers.GetDBUser(configuration)};Password={ConfigurationHelpers.GetDBPassword(configuration)}";

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

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "XSRF-TOKEN";
            options.Cookie.HttpOnly = false;
            options.Cookie.SecurePolicy = Enum.TryParse<CookieSecurePolicy>(ConfigurationHelpers.GetCookieSecurity(configuration), out var securePolicy) ? securePolicy : CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddControllers(options =>
            {
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            })
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

        services.AddHostedService<DailyScheduledTasksService>();

        services.AddScoped<IEmailService, EmailService>();

        services.AddScoped<IStripeService, StripeService>();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddSingleton<PrintBrokerService>();

        services.AddSingleton<ILoggerProvider>(sp =>
            new FileLoggerProvider(
                Path.Combine(builder.Environment.ContentRootPath, "logs"),
                sp.GetRequiredService<IConfiguration>(),
                builder.Environment.IsProduction() ? LogLevel.Warning : LogLevel.Debug));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "authentification";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Enum.TryParse<CookieSecurePolicy>(ConfigurationHelpers.GetCookieSecurity(configuration), out var securePolicy) ? securePolicy : CookieSecurePolicy.SameAsRequest;

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

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                options.ConfigureWarnings(warnings => warnings
                    .Throw(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.RowLimitingOperationWithoutOrderByWarning)
                    .Throw(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning)
                    .Throw(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning)
                );
            }
        });
    }

    private static async Task<WebApplication> UseDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var passwordHasher = services.GetRequiredService<IPasswordHasher<User>>();

            if (app.Environment.IsEnvironment("Testing"))
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
            await dbContext.Database.MigrateAsync();

            var initializer = new DbInitializer(dbContext, configuration, passwordHasher, app.Environment);
            await initializer.InitializeAsync();

            if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
            {
                var dataseeder = new DevelopmentDataSeeder(dbContext, passwordHasher);
                await dataseeder.SeedAsync();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            throw;
        }

        return app;
    }

    public static async Task ConfigurePipelineAsync(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        AppLog.Configure(app);

        AppLog.Log("test");

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            app.UseDeveloperExceptionPage();
        }

        await app.UseDatabaseMigrationsAsync();

        app.UseResponseCompression();

        app.UseCors("AllowBlazorClient");
        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAuthEndpoints();
        app.MapPrinterEndpoints();
        app.MapTemplateEndpoints();
        app.MapAgentEndpoints();
        app.MapPrintJobEndpoints();
        app.MapAgentWebSocketEndpoints();
        app.MapPrintEndpoints();
        app.MapUserEndpoints();
        app.MapTranslationEndpoints();
        app.MapLanguageEndpoints();
        app.MapAgentTypeEndpoints();
        app.MapProtocolEndpoints();
        app.MapResolutionDpiEndpoints();
        app.MapSubscriptionEndpoints();
        app.MapStripeEndpoints();
        app.MapClientEndpoints();
        app.MapAntiforgeryEndpoints();
        app.MapPlanEndpoints();
        app.MapAdminEndpoints();

        app.Run();
    }
}