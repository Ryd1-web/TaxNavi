using TaxNavi.Application.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

public class TaxAssistantTests
{
    private readonly GroundedTaxAssistantService _assistant = new(TestRules.Provider, TestRules.Engine);

    [Fact]
    public void BandAnswer_IsBuiltFromConfiguredRules()
    {
        var answer = _assistant.Answer(new AssistantQuestionDto { Question = "What are the tax bands?" });

        Assert.Equal("Tax bands", answer.Topic);
        Assert.Contains("₦800,000", answer.Answer);
        Assert.Contains("25%", answer.Answer);
        Assert.False(answer.RequiresProfessionalAdvice);
    }

    [Fact]
    public void RentAnswer_ForLegacyYear_ExplainsCra()
    {
        var answer = _assistant.Answer(new AssistantQuestionDto { Question = "Can I claim rent?", TaxYear = 2025 });
        Assert.Contains("not separately deductible", answer.Answer);
    }

    [Theory]
    [InlineData("What penalty applies if I file late?")]
    [InlineData("How is crypto taxed?")]
    [InlineData("I want to appeal an assessment")]
    public void SensitiveTopics_AreEscalatedToProfessionals(string question)
    {
        var answer = _assistant.Answer(new AssistantQuestionDto { Question = question });
        Assert.True(answer.RequiresProfessionalAdvice);
    }

    [Fact]
    public void UnknownQuestion_DoesNotGuess()
    {
        var answer = _assistant.Answer(new AssistantQuestionDto { Question = "What's the weather in Lagos?" });
        Assert.Equal("Not covered", answer.Topic);
        Assert.True(answer.RequiresProfessionalAdvice);
    }

    [Fact]
    public void ExplainMyResult_UsesEngineOutput()
    {
        var answer = _assistant.Answer(new AssistantQuestionDto
        {
            Question = "Explain my result",
            Calculation = TestRules.Salary(3_000_000m, 2026)
        });

        Assert.Equal("Your result", answer.Topic);
        Assert.Contains("₦330,000", answer.Answer);
    }
}
