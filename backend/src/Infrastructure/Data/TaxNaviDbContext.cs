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
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; set; }

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
            entity.Property(tr => tr.TaxLiability).HasPrecision(18, 2);
            entity.Property(tr => tr.TotalIncome).HasPrecision(18, 2);
            entity.Property(tr => tr.AllowableDeductions).HasPrecision(18, 2);
            entity.Property(tr => tr.ProjectedSavings).HasPrecision(18, 2);
            entity.Property(tr => tr.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(tr => tr.RuleSetId).HasMaxLength(64);
            entity.Property(tr => tr.GuestEmail).HasMaxLength(254);
            entity.Property(tr => tr.AccessTokenHash).HasMaxLength(64);
            entity.HasIndex(tr => new { tr.UserId, tr.CreatedAt });
            entity.HasOne(tr => tr.User)
                .WithMany(u => u.TaxReports)
                .HasForeignKey(tr => tr.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(tr => tr.TaxProfile)
                .WithMany(tp => tp.TaxReports)
                .HasForeignKey(tr => tr.TaxProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.Status).HasMaxLength(32).HasDefaultValue("Pending");
            entity.Property(p => p.Purpose).HasMaxLength(32);
            entity.Property(p => p.Currency).HasMaxLength(3);
            entity.Property(p => p.Reference).HasMaxLength(100);
            entity.HasIndex(p => p.Reference).IsUnique();
            entity.Property(p => p.Email).HasMaxLength(254);
            entity.Property(p => p.PlanCode).HasMaxLength(64);
            entity.Property(p => p.AuthorizationUrl).HasMaxLength(512);
            entity.Property(p => p.Channel).HasMaxLength(32);
            entity.Property(p => p.FailureReason).HasMaxLength(200);
            entity.Property(p => p.RowVersion).IsRowVersion();
            entity.HasIndex(p => p.TaxReportId);
            // At most one open (unpaid) checkout per report: blocks duplicate transactions from racing requests.
            entity.HasIndex(p => p.TaxReportId, "IX_Payments_TaxReportId_Pending")
                .IsUnique()
                .HasFilter("[Status] = 'Pending' AND [TaxReportId] IS NOT NULL");
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.HasOne(p => p.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Payments are financial history: they reference the report / subscription they paid for but are never
            // deleted with them. NO ACTION in the database (SQL Server forbids a second cascade path to Payments);
            // the application clears the link explicitly before deleting a report.
            entity.HasOne<TaxReport>()
                .WithMany()
                .HasForeignKey(p => p.TaxReportId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<Subscription>()
                .WithMany()
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Plan).HasMaxLength(32);
            entity.Property(s => s.Status).HasMaxLength(32);
            entity.Property(s => s.Amount).HasPrecision(18, 2);
            entity.Property(s => s.Currency).HasMaxLength(3).HasDefaultValue("NGN");
            entity.HasIndex(s => new { s.UserId, s.CurrentPeriodEnd });
            entity.Property(s => s.PlanCode).HasMaxLength(64);
            entity.Property(s => s.ProviderSubscriptionCode).HasMaxLength(64);
            entity.Property(s => s.ProviderEmailToken).HasMaxLength(64);
            entity.Property(s => s.ProviderCustomerCode).HasMaxLength(64);
            entity.HasIndex(s => s.ProviderSubscriptionCode).IsUnique().HasFilter("[ProviderSubscriptionCode] IS NOT NULL");
            entity.HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Webhook events: unique key gives idempotent processing.
        modelBuilder.Entity<PaymentWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(64);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.Outcome).HasMaxLength(64);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).HasDefaultValue("Received");
            entity.Property(e => e.LastError).HasMaxLength(1000);
            entity.HasIndex(e => e.EventKey).IsUnique();
            // Supports the retry worker's scan for unprocessed events.
            entity.HasIndex(e => new { e.Status, e.ReceivedAt });
        });
    }
}
