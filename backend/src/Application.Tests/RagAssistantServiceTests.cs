using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;
using Xunit;

namespace TaxNavi.Application.Tests;

/// <summary>Behavioural tests for the RAG assistant with faked LLM/retrieval and in-memory stores.</summary>
public class RagAssistantServiceTests
{
    private readonly FakeLlm _llm = new();
    private readonly FakeRetrieval _retrieval = new();
    private readonly FakeConversations _conversations = new();
    private readonly FakeUsage _usage = new();
    private readonly AiOptions _options = new();

    private RagAssistantService Create() => new(
        _llm,
        _retrieval,
        new GroundedTaxAssistantService(TestRules.Provider, TestRules.Engine),
        _conversations,
        _usage,
        new FakeUnitOfWork(),
        _options,
        TimeProvider.System,
        NullLogger<RagAssistantService>.Instance);

    private static AiChatRequestDto Ask(string question) => new() { Question = question };

    [Fact]
    public async Task EscalationTopics_NeverReachTheLlm()
    {
        var result = await Create().ChatAsync(null, "client", Ask("What penalty applies if I file late?"));

        Assert.Equal(AiOutcome.Ok, result.Outcome);
        Assert.Equal(AnswerGroundings.Escalation, result.Data!.Grounding);
        Assert.True(result.Data.RequiresProfessionalAdvice);
        Assert.Equal(0, _llm.Calls);
    }

    [Fact]
    public async Task Answer_CarriesCitations_ForMarkersTheModelUsed()
    {
        _retrieval.Hits = FakeRetrieval.Chunks("rent relief", "tax bands");
        _llm.NextText = "Rent relief is 20% of annual rent, capped. [S2] It reduces taxable income. [S2]";

        var result = await Create().ChatAsync(null, "client", Ask("How does rent relief work?"));

        Assert.Equal(AnswerGroundings.Retrieval, result.Data!.Grounding);
        var citation = Assert.Single(result.Data.Citations);
        Assert.Equal("S2", citation.Reference);
        Assert.Equal("Nigeria Tax Act 2025", citation.DocumentTitle);
        Assert.NotNull(result.Data.GuestToken); // first guest turn issues the conversation token
    }

    [Fact]
    public async Task InsufficientEvidenceSentinel_IsHonoured()
    {
        _retrieval.Hits = FakeRetrieval.Chunks("unrelated passage");
        _llm.NextText = $"{AiPrompts.InsufficientEvidenceSentinel}\nThe sources do not cover stamp duty rates.";

        var result = await Create().ChatAsync(null, "client", Ask("What is the stamp duty rate on leases?"));

        Assert.Equal(AnswerGroundings.InsufficientEvidence, result.Data!.Grounding);
        Assert.Equal("low", result.Data.Confidence);
        Assert.Empty(result.Data.Citations);
        Assert.DoesNotContain(AiPrompts.InsufficientEvidenceSentinel, result.Data.Answer);
    }

    [Fact]
    public async Task NoRetrievedEvidence_AnswersHonestly_WithoutLlm()
    {
        _retrieval.Hits = new List<RetrievedChunk>();

        var result = await Create().ChatAsync(null, "client", Ask("What is the tax treatment of carried interest?"));

        Assert.Equal(AnswerGroundings.InsufficientEvidence, result.Data!.Grounding);
        Assert.Equal(0, _llm.Calls);
    }

    [Fact]
    public async Task LlmFailure_FallsBackToRulesAssistant()
    {
        _retrieval.Hits = FakeRetrieval.Chunks("tax bands");
        _llm.Fail = true;

        var result = await Create().ChatAsync(null, "client", Ask("What are the tax bands?"));

        Assert.Equal(AiOutcome.Ok, result.Outcome);
        Assert.Equal(AnswerGroundings.Rules, result.Data!.Grounding);
        Assert.Contains("₦800,000", result.Data.Answer); // the deterministic answer
    }

    [Fact]
    public async Task UnconfiguredLlm_UsesRulesAssistant_AndStillPersists()
    {
        _llm.Configured = false;

        var result = await Create().ChatAsync(null, "client", Ask("What deductions can I claim?"));

        Assert.Equal(AnswerGroundings.Rules, result.Data!.Grounding);
        var conversation = Assert.Single(_conversations.Store.Values);
        Assert.Equal(2, conversation.Messages.Count); // question + answer saved
    }

    [Fact]
    public async Task GuestQuota_IsEnforcedBeforeAnyPaidCall()
    {
        _usage.Count = _options.DailyMessagesPerGuest;

        var result = await Create().ChatAsync(null, "client", Ask("How is PAYE calculated?"));

        Assert.Equal(AiOutcome.QuotaExceeded, result.Outcome);
        Assert.Equal(0, _llm.Calls);
    }

    [Fact]
    public async Task ForeignConversation_IsNotFound_NotForbidden()
    {
        var service = Create();
        var owner = Guid.NewGuid();
        var first = await service.ChatAsync(owner, null, Ask("What are the tax bands?"));
        var intruder = await service.ChatAsync(Guid.NewGuid(), null, new AiChatRequestDto
        {
            ConversationId = first.Data!.ConversationId,
            Question = "And deductions?"
        });

        Assert.Equal(AiOutcome.NotFound, intruder.Outcome);
    }

    [Fact]
    public async Task GuestConversation_RequiresItsToken()
    {
        _retrieval.Hits = FakeRetrieval.Chunks("rent relief");
        _llm.NextText = "Answer. [S1]";
        var service = Create();

        var first = await service.ChatAsync(null, "client", Ask("How does rent relief work?"));
        var withToken = await service.GetConversationAsync(null, first.Data!.GuestToken, first.Data.ConversationId);
        var withoutToken = await service.GetConversationAsync(null, "wrong-token", first.Data.ConversationId);

        Assert.Equal(AiOutcome.Ok, withToken.Outcome);
        Assert.Equal(2, withToken.Data!.Messages.Count);
        Assert.Equal(AiOutcome.NotFound, withoutToken.Outcome);
    }

    [Fact]
    public async Task FollowUp_ReplaysHistory_ToTheModel()
    {
        _retrieval.Hits = FakeRetrieval.Chunks("deductions for freelancers");
        _llm.NextText = "Freelancers can deduct business expenses. [S1]";
        var service = Create();
        var first = await service.ChatAsync(null, "client", Ask("What are allowable deductions for freelancers?"));

        _llm.NextText = "Examples include internet and equipment costs. [S1]";
        await service.ChatAsync(null, "client", new AiChatRequestDto
        {
            ConversationId = first.Data!.ConversationId,
            GuestToken = first.Data.GuestToken,
            Question = "Can you give me examples?"
        });

        Assert.Contains(_llm.LastRequest!.Messages, m => m.Text.Contains("allowable deductions for freelancers"));
        Assert.Contains("Never invent tax law", _llm.LastRequest.SystemInstruction);
    }

    // ----- fakes -----

    private sealed class FakeLlm : ILlmService
    {
        public bool Configured = true;
        public bool Fail;
        public string NextText = "Grounded answer. [S1]";
        public int Calls;
        public LlmRequest? LastRequest;

        public bool IsConfigured => Configured;
        public string ModelName => "fake-model";

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(Fail
                ? new LlmResponse { Success = false, Error = "provider", Model = ModelName }
                : new LlmResponse { Success = true, Text = NextText, Model = ModelName, InputTokens = 100, OutputTokens = 50 });
        }
    }

    private sealed class FakeRetrieval : IRetrievalService
    {
        public List<RetrievedChunk> Hits = Chunks("default passage");

        public Task<List<RetrievedChunk>> RetrieveAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Hits);

        public static List<RetrievedChunk> Chunks(params string[] contents)
        {
            var document = new KnowledgeDocument
            {
                Id = Guid.NewGuid(),
                Title = "Nigeria Tax Act 2025",
                DocumentType = KnowledgeDocumentTypes.Legislation,
                SourceName = "National Assembly",
                SourceUrl = "https://nass.gov.ng/example",
                Status = KnowledgeDocumentStatuses.Indexed
            };
            return contents.Select((content, i) => new RetrievedChunk(
                new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    ChunkIndex = i,
                    Content = content,
                    SectionNumber = (i + 1).ToString(),
                    PageFrom = i + 10,
                    PageTo = i + 10,
                    TokenCount = content.Length / 4,
                    Embedding = new Vector(new float[4])
                },
                document,
                0.72 - i * 0.05)).ToList();
        }
    }

    private sealed class FakeConversations : IChatConversationRepository
    {
        public readonly Dictionary<Guid, ChatConversation> Store = new();

        public Task<ChatConversation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.GetValueOrDefault(id));

        public Task<ChatConversation?> GetWithMessagesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Store.GetValueOrDefault(id));

        public Task<List<ChatConversation>> ListForUserAsync(Guid userId, int take, CancellationToken ct = default) =>
            Task.FromResult(Store.Values.Where(c => c.UserId == userId).Take(take).ToList());

        public Task AddAsync(ChatConversation conversation, CancellationToken ct = default)
        {
            Store[conversation.Id] = conversation;
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
        {
            Store[message.ConversationId].Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ChatConversation conversation, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> NextOrdinalAsync(Guid conversationId, CancellationToken ct = default) =>
            Task.FromResult(Store[conversationId].Messages.Count + 1);
    }

    private sealed class FakeUsage : IAiUsageRepository
    {
        public int Count;
        public readonly List<AiUsageRecord> Records = new();

        public Task AddAsync(AiUsageRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<int> CountSinceAsync(Guid? userId, string? clientKey, DateTime since, CancellationToken ct = default) =>
            Task.FromResult(Count);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default) => action(ct);

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default) => action(ct);
    }
}
