namespace FastLabel.Backend.Tests.Extensions;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IEmailService> EmailServiceMock { get; } = new();
    public Mock<IStripeService> StripeServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        IConfiguration? configuration = null;

        builder.ConfigureAppConfiguration((context, config) =>
        {
            AddEnvironmentConfiguration(config);  
            configuration = config.Build();
        });

        builder.ConfigureLogging(logging =>
        {
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Critical);
        });

        builder.ConfigureTestServices(services =>
        {
            var connectionString = configuration?.GetConnectionString("DefaultConnection")
                ?? $"Host=localhost;Port=5432;Database={configuration?["DB_NAME"] ?? string.Empty};Username={configuration?["DB_USER"] ?? string.Empty};Password={configuration?["DB_PASSWORD"] ?? string.Empty}";

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
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

    private static IConfigurationBuilder AddEnvironmentConfiguration(IConfigurationBuilder config)
    {
        var fileName = ".env.test";
        
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else
        {
            var parentDir = Directory.GetParent(Directory.GetCurrentDirectory());
            while (parentDir != null)
            {
                var candidate = Path.Combine(parentDir.FullName, fileName);
                if (File.Exists(candidate))
                {
                    Env.Load(candidate);
                    break;
                }
                parentDir = parentDir.Parent;
            }
        }

        EnvironmentVariablesExtensions.AddEnvironmentVariables(config);

        return config;
    }
}