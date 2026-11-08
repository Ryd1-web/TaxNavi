namespace TaxNavi.Infrastructure.Services;

/// <summary>Paystack configuration (section "Paystack"). The secret key must come from user secrets or the environment.</summary>
public class PaystackOptions
{
    public string? SecretKey { get; set; }
    public string? PublicKey { get; set; }

    /// <summary>Plan code (PLN_…) for the SME monthly subscription. Empty disables recurring billing.</summary>
    public string? SmePlanCode { get; set; }

    /// <summary>Live keys (sk_live_) are refused unless this is explicitly true.</summary>
    public bool AllowLiveKeys { get; set; }

    /// <summary>Override for automated tests against a local stub. Defaults to the Paystack API.</summary>
    public string BaseUrl { get; set; } = "https://api.paystack.co/";
}
