namespace TaxNavi.Application.DTOs;

/// <summary>
/// Retained for API compatibility. Access flags supplied by the client are ignored:
/// entitlement is always resolved server-side.
/// </summary>
public class FullTaxCalculationRequestDto
{
    public TaxCalculationRequestDto Input { get; set; } = new();
    public TaxAccessRequestDto Access { get; set; } = new();
}
