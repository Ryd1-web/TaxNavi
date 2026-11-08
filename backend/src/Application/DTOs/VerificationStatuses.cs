namespace TaxNavi.Application.DTOs;

public static class VerificationStatuses
{
    /// <summary>Checked against official legislation or NRS guidance.</summary>
    public const string Verified = "Verified";

    /// <summary>Consistent across multiple reputable secondary sources; official text still to be confirmed.</summary>
    public const string SecondarySourcesVerified = "SecondarySourcesVerified";

    /// <summary>Sources conflict or are unverified. Shown to users with a caution.</summary>
    public const string PendingValidation = "PendingValidation";
}

// ---------------------------------------------------------------------------
// Request
// ---------------------------------------------------------------------------
