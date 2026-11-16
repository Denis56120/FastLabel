namespace FastLabel.Backend.Tests.Extensions;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Mock<IEmailService> EmailServiceMock { get; } = new();
    public Mock<IStripeService> StripeServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // 1. Charge ton fichier .env et construit la configuration
        IConfiguration? configuration = null;

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Charge le .env s'il existe à la racine
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
            }
            else
            {
                // Recherche alternative si on est dans bin/Debug/net10.0
                var parentDir = Directory.GetParent(Directory.GetCurrentDirectory());
                while (parentDir != null)
                {
                    var candidate = Path.Combine(parentDir.FullName, ".env");
                    if (File.Exists(candidate))
                    {
                        Env.Load(candidate);
                        break;
                    }
                    parentDir = parentDir.Parent;
                }
            }

            // Permet aussi de lire les variables d'environnement système/chargées par DotNetEnv
            config.AddEnvironmentVariables();

            configuration = config.Build();
        });

        builder.ConfigureTestServices(services =>
        {
            // 2. Récupération sécurisée de la chaîne de connexion via la configuration
            var connectionString = configuration?.GetConnectionString("DefaultConnection")
                ?? $"Host=localhost;Port=5432;Database={configuration?["DB_NAME"] ?? string.Empty};Username={configuration?["DB_USER"] ?? string.Empty};Password={configuration?["DB_PASSWORD"] ?? string.Empty}";

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });

            services.RemoveAll<IEmailService>();
            services.AddSingleton(EmailServiceMock.Object);

            services.RemoveAll<IStripeService>();
            services.AddSingleton(StripeServiceMock.Object);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Nettoie et applique les migrations
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        // 2. Initialise les données de référence (BasicStatuses, Languages, etc.)
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var initializer = new DbInitializer(db, configuration, passwordHasher);
        await initializer.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}