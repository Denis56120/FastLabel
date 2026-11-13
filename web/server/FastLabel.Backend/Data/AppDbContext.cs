namespace FastLabel.Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Text> Texts => Set<Text>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<Protocol> Protocols => Set<Protocol>();
    public DbSet<ResolutionDpi> ResolutionDpis => Set<ResolutionDpi>();
    public DbSet<BasicStatus> BasicStatuses => Set<BasicStatus>();
    public DbSet<PrintJobStatus> PrintJobStatuses => Set<PrintJobStatus>();
    public DbSet<SubscriptionType> SubscriptionTypes => Set<SubscriptionType>();
    public DbSet<AgentType> AgentTypes => Set<AgentType>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Stripe> Stripes => Set<Stripe>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<Agent> Agents => Set<Agent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}