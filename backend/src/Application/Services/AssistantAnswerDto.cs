using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public class AssistantAnswerDto
{
    public string Topic { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public bool RequiresProfessionalAdvice { get; set; }
    public string Grounding { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
    public List<TaxSourceDto> Sources { get; set; } = new();
    public List<string> SuggestedQuestions { get; set; } = new();
}
