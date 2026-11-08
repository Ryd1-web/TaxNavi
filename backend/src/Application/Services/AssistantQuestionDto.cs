using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public class AssistantQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public int? TaxYear { get; set; }

    /// <summary>Optional: the user's own calculation input, recomputed server-side for context.</summary>
    public TaxCalculationRequestDto? Calculation { get; set; }
}
