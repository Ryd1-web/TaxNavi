namespace TaxNavi.Application.Services;

public interface ITaxAssistantService
{
    AssistantAnswerDto Answer(AssistantQuestionDto question);
}
