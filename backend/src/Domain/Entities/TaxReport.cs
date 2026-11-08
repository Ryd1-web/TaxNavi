namespace TaxNavi.Domain.Entities;

public class TaxReport
{
    public Guid Id { get; set; }

    /// <summary>Owner. Null for a guest report, which is accessed with its access token instead.</summary>
    public Guid? UserId { get; set; }
    public Guid TaxProfileId { get; set; }
    public string PdfUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal TaxLiability { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal ProjectedSavings { get; set; }
    public string ReportType { get; set; } = "Individual"; // Individual, SelfEmployed or SME
    public int TaxYear { get; set; }
    public string RuleSetId { get; set; } = string.Empty;
    public string JsonData { get; set; } = "{}"; // Full report snapshot at the time of calculation

    /// <summary>Email captured at guest checkout (receipt and recovery contact).</summary>
    public string? GuestEmail { get; set; }

    /// <summary>SHA-256 hash of the guest access token. The token itself is never stored.</summary>
    public string? AccessTokenHash { get; set; }

    /// <summary>Set only after a server-verified one-off purchase.</summary>
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public TaxProfile? TaxProfile { get; set; }
}
