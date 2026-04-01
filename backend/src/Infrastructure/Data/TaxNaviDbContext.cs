namespace TaxNavi.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;

public class TaxNaviDbContext : DbContext
{
    public TaxNaviDbContext(DbContextOptions<TaxNaviDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<TaxProfile> TaxProfiles { get; set; }
    public DbSet<TaxReport> TaxReports { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // TaxProfile configuration
        modelBuilder.Entity<TaxProfile>(entity =>
        {
            entity.HasKey(tp => tp.Id);
            entity.Property(tp => tp.JsonData).HasDefaultValue("{}");
            entity.Property(tp => tp.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(tp => tp.User)
                .WithMany(u => u.TaxProfiles)
                .HasForeignKey(tp => tp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaxReport configuration
        modelBuilder.Entity<TaxReport>(entity =>
        {
            entity.HasKey(tr => tr.Id);
            entity.Property(tr => tr.JsonData).HasDefaultValue("{}");
            entity.Property(tr => tr.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(tr => tr.User)
                .WithMany(u => u.TaxReports)
                .HasForeignKey(tr => tr.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(tr => tr.TaxProfile)
                .WithMany(tp => tp.TaxReports)
                .HasForeignKey(tr => tr.TaxProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Status).HasDefaultValue("Pending");
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
