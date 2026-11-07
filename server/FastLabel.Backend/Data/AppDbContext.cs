using FastLabel.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FastLabel.Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.ApiKey).IsUnique();
        });

        modelBuilder.Entity<PrintJob>(entity =>
        {
            entity.HasKey(p => p.Id);
        });

        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(p => p.Id);
        });

        modelBuilder.Entity<LabelTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.StripeCustomerId);
        });
    }
}