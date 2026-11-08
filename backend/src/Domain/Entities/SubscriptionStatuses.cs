namespace TaxNavi.Domain.Entities;

public static class SubscriptionStatuses
{
    public const string Active = "Active";

    /// <summary>Cancelled by the customer; access continues until the paid period ends.</summary>
    public const string NonRenewing = "NonRenewing";

    /// <summary>Renewal charge failed; access continues for the configured grace period.</summary>
    public const string PastDue = "PastDue";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}
