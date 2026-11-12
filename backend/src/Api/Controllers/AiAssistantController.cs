using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;

namespace TaxNavi.Api.Controllers;

/// <summary>
/// RAG-powered Nigerian tax assistant. Available to guests (with per-client quotas and a
/// conversation bearer token) and signed-in users (with saved history).
/// </summary>
[Route("api/ai/assistant")]
[EnableRateLimiting("ai")]
public class AiAssistantController : ApiControllerBase
{
    /// <summary>Header that carries a guest conversation token on reads.</summary>
    public const string ConversationTokenHeader = "X-Conversation-Token";

    private readonly IAiAssistantService _assistant;
    private readonly IReportExplanationService _explainer;
    private readonly ITaxCalculationEngine _engine;

    public AiAssistantController(IAiAssistantService assistant, IReportExplanationService explainer, ITaxCalculationEngine engine)
    {
        _assistant = assistant;
        _explainer = explainer;
        _engine = engine;
    }

    /// <summary>Ask the tax assistant a question (creates or continues a conversation).</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
    {
        Guid? userId = TryGetUserId(out var id) ? id : null;
        var result = await _assistant.ChatAsync(userId, userId == null ? ClientKey() : null, request, HttpContext.RequestAborted);
        return Map(result);
    }

    /// <summary>The signed-in user's conversation history.</summary>
    [Authorize]
    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations()
    {
        if (!TryGetUserId(out var userId))
        {
            return Failure(StatusCodes.Status401Unauthorized, "Sign in to view conversations.");
        }

        return Success(await _assistant.ListConversationsAsync(userId, HttpContext.RequestAborted));
    }

    /// <summary>One conversation with messages. Guests pass their token in X-Conversation-Token.</summary>
    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Conversation(Guid id)
    {
        Guid? userId = TryGetUserId(out var uid) ? uid : null;
        var token = Request.Headers[ConversationTokenHeader].ToString();
        var result = await _assistant.GetConversationAsync(userId, token, id, HttpContext.RequestAborted);
        return Map(result);
    }

    /// <summary>
    /// Plain-English explanation of a tax calculation. The engine recomputes the figures
    /// server-side from the calculator input; the AI never calculates anything.
    /// </summary>
    [HttpPost("explain-report")]
    public async Task<IActionResult> ExplainReport([FromBody] ExplainReportRequestDto request)
    {
        if (request.Calculation.IncomeSources.Count == 0)
        {
            return Failure(StatusCodes.Status400BadRequest, "Run a calculation first, then ask for an explanation.");
        }

        var report = _engine.GenerateFullReport(request.Calculation);
        Guid? userId = TryGetUserId(out var id) ? id : null;
        var result = await _explainer.ExplainAsync(userId, userId == null ? ClientKey() : null, report, HttpContext.RequestAborted);
        return Map(result);
    }

    /// <summary>Stable, non-reversible key for anonymous quota tracking (never a raw IP in the database).</summary>
    private string ClientKey()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("taxnavi-ai:" + ip)));
    }

    private IActionResult Map<T>(AiResult<T> result) => result.Outcome switch
    {
        AiOutcome.Ok => Success(result.Data),
        AiOutcome.QuotaExceeded => Failure(StatusCodes.Status429TooManyRequests, result.Message ?? "Daily AI limit reached.", "AI_QUOTA"),
        AiOutcome.NotFound => Failure(StatusCodes.Status404NotFound, result.Message ?? "Not found."),
        _ => Failure(StatusCodes.Status400BadRequest, result.Message ?? "Invalid request.")
    };
}
