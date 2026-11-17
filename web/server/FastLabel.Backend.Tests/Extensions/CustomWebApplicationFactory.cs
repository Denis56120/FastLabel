namespace FastLabel.Backend.Tests.Extensions;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public Mock<IEmailService> EmailServiceMock { get; } = new();
    public Mock<IStripeService> StripeServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        IConfiguration? configuration = null;

        builder.ConfigureAppConfiguration((context, config) =>
        {
            ServiceExtensions.AddEnvironmentConfiguration();

            config.AddEnvironmentVariables();

            configuration = config.Build();
        });

        builder.ConfigureLogging(logging =>
        {
            // Ignore les logs de connexion transitoires [20004] d'EF Core qui polluent la sortie de test
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Critical);
        });

        builder.ConfigureTestServices(services =>
        {
            // 2. Récupération sécurisée de la chaîne de connexion via la configuration
            var connectionString = configuration?.GetConnectionString("DefaultConnection")
                ?? $"Host=localhost;Port=5432;Database={configuration?["DB_NAME"] ?? string.Empty};Username={configuration?["DB_USER"] ?? string.Empty};Password={configuration?["DB_PASSWORD"] ?? string.Empty}";

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Fractionnement des requêtes
                    npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                })
                .ConfigureWarnings(warnings => warnings
                    .Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning)
                    .Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning)
                    .Throw(RelationalEventId.MultipleCollectionIncludeWarning)
                )
            );

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

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var configuration = Services.GetRequiredService<IConfiguration>();
        var initializer = new DbInitializer(db, configuration, passwordHasher);
        await initializer.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}