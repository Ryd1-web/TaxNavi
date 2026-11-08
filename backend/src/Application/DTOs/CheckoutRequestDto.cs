namespace TaxNavi.Application.DTOs;

public class CheckoutRequestDto
{
    /// <summary>PremiumReport or SmeSubscription.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>An existing report to unlock (owned by the caller, or a guest report with <see cref="AccessToken"/>).</summary>
    public Guid? TaxReportId { get; set; }

    /// <summary>Guest checkout: the report access token issued when the guest report was created.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Guest checkout without a saved report: the calculation is re-run and saved server-side.</summary>
    public TaxCalculationRequestDto? Calculation { get; set; }

    /// <summary>Required for guests; ignored for signed-in users (their account email is used).</summary>
    public string? Email { get; set; }
}
