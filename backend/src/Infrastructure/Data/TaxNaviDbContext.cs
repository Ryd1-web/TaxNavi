namespace TaxNavi.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using TaxNavi.Domain.Entities;

public class TaxNaviDbContext : DbContext
{
    static TaxNaviDbContext()
    {
        NpgsqlSettings.Configure();
    }

    public TaxNaviDbContext(DbContextOptions<TaxNaviDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<TaxProfile> TaxProfiles { get; set; }
    public DbSet<TaxReport> TaxReports { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; set; }
    public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
    public DbSet<DocumentChunk> DocumentChunks { get; set; }
    public DbSet<ChatConversation> ChatConversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<AiUsageRecord> AiUsageRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // pgvector: semantic search over knowledge-base chunks. The InMemory provider used by
        // API tests cannot map Vector, so vector mapping is gated on the real provider.
        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
        });

        // TaxProfile configuration
        modelBuilder.Entity<TaxProfile>(entity =>
        {
            entity.HasKey(tp => tp.Id);
            entity.Property(tp => tp.JsonData).HasDefaultValue("{}");
            entity.Property(tp => tp.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
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
            entity.Property(tr => tr.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
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
            entity.Property(p => p.Version).IsRowVersion(); // maps to PostgreSQL's xmin system column
            entity.HasIndex(p => p.TaxReportId);
            // At most one open (unpaid) checkout per report: blocks duplicate transactions from racing requests.
            entity.HasIndex(p => p.TaxReportId, "IX_Payments_TaxReportId_Pending")
                .IsUnique()
                .HasFilter("\"Status\" = 'Pending' AND \"TaxReportId\" IS NOT NULL");
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
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
            entity.HasIndex(s => s.ProviderSubscriptionCode).IsUnique().HasFilter("\"ProviderSubscriptionCode\" IS NOT NULL");
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
        // ----- AI knowledge base & assistant -----

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(300);
            entity.Property(d => d.DocumentType).IsRequired().HasMaxLength(32);
            entity.Property(d => d.SourceName).HasMaxLength(200);
            entity.Property(d => d.SourceUrl).HasMaxLength(1000);
            entity.Property(d => d.ContentHash).IsRequired().HasMaxLength(64);
            entity.Property(d => d.Status).IsRequired().HasMaxLength(32);
            entity.Property(d => d.LastError).HasMaxLength(1000);
            entity.Property(d => d.FileName).HasMaxLength(260);
            entity.Property(d => d.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
            entity.HasIndex(d => d.ContentHash).IsUnique();   // the same PDF is never ingested twice
            entity.HasIndex(d => new { d.Title, d.Version });
            entity.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Content).IsRequired();
            entity.Property(c => c.Chapter).HasMaxLength(200);
            entity.Property(c => c.Part).HasMaxLength(200);
            entity.Property(c => c.SectionNumber).HasMaxLength(16);
            entity.Property(c => c.SectionTitle).HasMaxLength(200);
            if (Database.IsNpgsql())
            {
                entity.Property(c => c.Embedding).HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})");
                // HNSW: approximate nearest-neighbour search by cosine distance.
                entity.HasIndex(c => c.Embedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops");
            }
            else
            {
                entity.Ignore(c => c.Embedding);
            }

            entity.Property(c => c.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
            entity.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique();
        });

        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
            entity.Property(c => c.GuestTokenHash).HasMaxLength(64);
            entity.Property(c => c.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);  // deleting an account removes their chats
            entity.HasIndex(c => new { c.UserId, c.CreatedAt });
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).IsRequired().HasMaxLength(16);
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.CitationsJson).HasColumnType("jsonb");
            entity.Property(m => m.Grounding).HasMaxLength(32);
            entity.Property(m => m.Model).HasMaxLength(100);
            entity.Property(m => m.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
            entity.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.ConversationId, m.Ordinal }).IsUnique();
        });

        modelBuilder.Entity<AiUsageRecord>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.ClientKey).HasMaxLength(64);
            entity.Property(r => r.Operation).IsRequired().HasMaxLength(32);
            entity.Property(r => r.Model).HasMaxLength(100);
            entity.Property(r => r.ErrorCategory).HasMaxLength(32);
            entity.Property(r => r.CreatedAt).HasDefaultValueSql("(now() at time zone 'utc')");
            // Quota checks: "how many successful calls since midnight for this user/client?"
            entity.HasIndex(r => new { r.UserId, r.CreatedAt });
            entity.HasIndex(r => new { r.ClientKey, r.CreatedAt });
        });
    }
}
