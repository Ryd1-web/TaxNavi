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
}

public class TaxProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ProfileType { get; set; } = "Individual"; // Individual or SME
    public string JsonData { get; set; } = "{}"; // Stores individual or SME specific data
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public ICollection<TaxReport> TaxReports { get; set; } = new List<TaxReport>();
}

public class TaxReport
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TaxProfileId { get; set; }
    public string PdfUrl { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public decimal TaxLiability { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal ProjectedSavings { get; set; }
    public string ReportType { get; set; } = "Individual"; // Individual or SME
    public string JsonData { get; set; } = "{}"; // Detailed report data
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
    public TaxProfile? TaxProfile { get; set; }
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? TaxReportId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
    public string Reference { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Paystack";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
