namespace TaxNavi.Application.DTOs;

/// <summary>Outcome codes that controllers translate into HTTP responses.</summary>
public enum BillingOutcome
{
    Ok,
    NotFound,
    Invalid,
    Unauthorized,
    AlreadyEntitled,
    PaymentsNotConfigured,
    ProviderError,
    Pending,
    Cancelled,
    Failed
}
