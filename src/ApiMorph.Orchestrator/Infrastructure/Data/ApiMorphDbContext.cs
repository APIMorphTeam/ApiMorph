using ApiMorph.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiMorph.Orchestrator.Infrastructure.Data;

public class ApiMorphDbContext(DbContextOptions<ApiMorphDbContext> options) : DbContext(options)
{
    public DbSet<Installation> Installations => Set<Installation>();

    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<ScanJob> ScanJobs => Set<ScanJob>();

    public DbSet<Finding> Findings => Set<Finding>();

    public DbSet<AutomationJob> AutomationJobs => Set<AutomationJob>();

    public DbSet<ProviderFeedState> ProviderFeedStates => Set<ProviderFeedState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Installation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitHubOwner).HasMaxLength(256).IsRequired();
            entity.Property(e => e.GitHubRepo).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DefaultBranch).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Providers).HasMaxLength(512).HasDefaultValue("stripe");
            entity.Property(e => e.WebhookBranches).HasMaxLength(512);
            entity.Property(e => e.ScheduleCron).HasMaxLength(128);
            entity.HasIndex(e => new { e.GitHubOwner, e.GitHubRepo }).IsUnique();
            entity.HasOne(e => e.Installation)
                .WithMany(i => i.Repositories)
                .HasForeignKey(e => e.InstallationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Error).HasMaxLength(4096);
            entity.Property(e => e.RepositoryPath).HasMaxLength(2048);
            entity.Property(e => e.BranchName).HasMaxLength(512);
            entity.Property(e => e.PullRequestUrl).HasMaxLength(2048);
            entity.Property(e => e.PatchMode).HasMaxLength(32).HasDefaultValue("detect-only");
            entity.Property(e => e.PatchesJson).HasMaxLength(16384);
            entity.HasOne(e => e.Repository)
                .WithMany(r => r.ScanJobs)
                .HasForeignKey(e => e.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(4096).IsRequired();
            entity.Property(e => e.Evidence).HasMaxLength(4096);
            entity.Property(e => e.Confidence).HasConversion<string>().HasMaxLength(16);
            entity.HasOne(e => e.ScanJob)
                .WithMany(j => j.Findings)
                .HasForeignKey(e => e.ScanJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomationJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitHubOwner).HasMaxLength(256).IsRequired();
            entity.Property(e => e.GitHubRepo).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Language).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Trigger).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Branch).HasMaxLength(512);
            entity.Property(e => e.CommitSha).HasMaxLength(64);
            entity.Property(e => e.DedupeKey).HasMaxLength(512);
            entity.Property(e => e.Error).HasMaxLength(4096);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DedupeKey);
        });

        modelBuilder.Entity<ProviderFeedState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Provider).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Fingerprint).HasMaxLength(128);
            entity.HasIndex(e => e.Provider).IsUnique();
        });
    }
}
