namespace TaxNavi.Application.DTOs;

public class TaxAccessRequestDto
{
    public bool IsPremiumUser { get; set; }
    public bool HasPaidForReport { get; set; }
    public bool SaveToHistory { get; set; }
    public bool RequestPdfExport { get; set; }
    public bool IsAuthenticatedUser { get; set; }
    public string Email { get; set; } = string.Empty;
}

// ---------------------------------------------------------------------------
// Response
// ---------------------------------------------------------------------------
