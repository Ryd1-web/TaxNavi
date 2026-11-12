namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Administrators, configured by email ("App:Admin:Emails"). Admins get the "Admin" role
/// claim at sign-in, which protects knowledge-base management endpoints.
/// </summary>
public class AdminOptions
{
    public List<string> Emails { get; set; } = new();

    public bool IsAdmin(string email) =>
        Emails.Any(e => string.Equals(e?.Trim(), email, StringComparison.OrdinalIgnoreCase));
}
