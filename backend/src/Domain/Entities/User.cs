namespace TaxNavi.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string UserType { get; set; } = "Individual"; // Individual or SME
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<TaxProfile> TaxProfiles { get; set; } = new List<TaxProfile>();
    public ICollection<TaxReport> TaxReports { get; set; } = new List<TaxReport>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
