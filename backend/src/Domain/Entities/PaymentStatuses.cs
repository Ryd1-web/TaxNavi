namespace TaxNavi.Domain.Entities;

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    /// <summary>Customer cancelled or abandoned checkout. A later successful charge can still complete it.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>Superseded by a newer checkout for the same item before being paid.</summary>
    public const string Expired = "Expired";
}
