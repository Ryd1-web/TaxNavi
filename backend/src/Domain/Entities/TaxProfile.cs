namespace TaxNavi.Domain.Entities;

public class TaxProfile
{
    public Guid Id { get; set; }

    /// <summary>Null for guest (anonymous) purchases.</summary>
    public Guid? UserId { get; set; }
    public string ProfileType { get; set; } = "Individual"; // Individual, SelfEmployed or SME
    public string JsonData { get; set; } = "{}"; // Calculation input snapshot
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public ICollection<TaxReport> TaxReports { get; set; } = new List<TaxReport>();
}
