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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Printer>(entity =>
        {
            entity.HasKey(p => p.Id);
        });

        modelBuilder.Entity<LabelTemplate>(entity =>
        {
            entity.HasKey(t => t.Id);
        });
    }
}