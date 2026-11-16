namespace FastLabel.Backend.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestConnectionString = "Host=localhost;Database=FastLabel_Test;Username=postgres;Password=postgres";

    public Mock<EmailService> EmailServiceMock { get; } = new();
    public Mock<StripeService> StripeServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(TestConnectionString);
            });

            services.RemoveAll<EmailService>();
            services.AddSingleton(EmailServiceMock.Object);

            services.RemoveAll<StripeService>();
            services.AddSingleton(StripeServiceMock.Object);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        // Pense à appeler ici ton initialiseur de données de base (langues, statuts, textes)
        // ex: await DbInitializer.InitializeAsync(db);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}