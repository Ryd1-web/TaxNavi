using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers;

/// <summary>
/// Admin-only knowledge-base management: upload legislation/guidance PDFs, trigger indexing,
/// inspect status. Admins are configured by email (App:Admin:Emails) and carry the Admin role.
/// </summary>
[Route("api/admin/knowledge-base")]
[Authorize(Roles = "Admin")]
public class KnowledgeBaseController : ApiControllerBase
{
    private readonly IDocumentIngestionService _ingestion;
    private readonly IKnowledgeDocumentRepository _documents;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(IDocumentIngestionService ingestion, IKnowledgeDocumentRepository documents, ILogger<KnowledgeBaseController> logger)
    {
        _ingestion = ingestion;
        _documents = documents;
        _logger = logger;
    }

    public class UploadDocumentForm
    {
        public IFormFile File { get; set; } = null!;
        public string Title { get; set; } = string.Empty;

        /// <summary>"Legislation" (binding) or "Guidance" (explanatory).</summary>
        public string DocumentType { get; set; } = KnowledgeDocumentTypes.Legislation;

        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public int? TaxYear { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? PublicationDate { get; set; }

        /// <summary>Index immediately after upload (default true).</summary>
        public bool Index { get; set; } = true;
    }

    [HttpPost("documents")]
    [RequestSizeLimit(41_943_040)] // 40 MB
    public async Task<IActionResult> Upload([FromForm] UploadDocumentForm form)
    {
        if (form.File == null || form.File.Length == 0)
        {
            return Failure(StatusCodes.Status400BadRequest, "Attach a PDF file.");
        }

        if (string.IsNullOrWhiteSpace(form.Title))
        {
            return Failure(StatusCodes.Status400BadRequest, "Title is required.");
        }

        if (form.DocumentType is not (KnowledgeDocumentTypes.Legislation or KnowledgeDocumentTypes.Guidance))
        {
            return Failure(StatusCodes.Status400BadRequest, "DocumentType must be Legislation or Guidance.");
        }

        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer, HttpContext.RequestAborted);

        var document = await _ingestion.RegisterAsync(new KnowledgeDocument
        {
            Title = form.Title.Trim(),
            DocumentType = form.DocumentType,
            SourceName = form.SourceName.Trim(),
            SourceUrl = form.SourceUrl.Trim(),
            TaxYear = form.TaxYear,
            EffectiveDate = form.EffectiveDate,
            PublicationDate = form.PublicationDate,
            FileName = Path.GetFileName(form.File.FileName)
        }, buffer.ToArray(), HttpContext.RequestAborted);

        if (form.Index && document.Status == KnowledgeDocumentStatuses.Pending)
        {
            try
            {
                document = await _ingestion.IndexAsync(document.Id, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // Registration succeeded; the document can be re-indexed after fixing the cause.
                _logger.LogError(ex, "Indexing failed after upload for {DocumentId}", document.Id);
            }
        }

        return Success(ToStatus(document), document.Status == KnowledgeDocumentStatuses.Indexed
            ? "Document indexed."
            : $"Document registered (status: {document.Status}).");
    }

    [HttpPost("documents/{id:guid}/index")]
    public async Task<IActionResult> Index(Guid id)
    {
        try
        {
            var document = await _ingestion.IndexAsync(id, HttpContext.RequestAborted);
            return Success(ToStatus(document), "Document indexed.");
        }
        catch (KeyNotFoundException)
        {
            return Failure(StatusCodes.Status404NotFound, "Document not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed for {DocumentId}", id);
            return Failure(StatusCodes.Status502BadGateway, "Indexing failed — check the document status for details.", "INDEXING_FAILED");
        }
    }

    /// <summary>
    /// Run the repeatable evaluation set (knowledge-base/eval.json) against the live index.
    /// Retrieval metrics are free; <c>generateAnswers=true</c> also calls the LLM once per case
    /// to check grounding, citation validity and injection resistance (costs tokens).
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate(
        [FromServices] RagEvaluationService evaluation,
        [FromServices] IConfiguration configuration,
        [FromServices] IHostEnvironment environment,
        [FromQuery] bool generateAnswers = false)
    {
        var path = BackgroundServices.KnowledgeBasePaths.Resolve(configuration, environment, "eval.json", "KnowledgeBase:EvalPath");
        if (path == null)
        {
            return Failure(StatusCodes.Status404NotFound, "knowledge-base/eval.json was not found on this host.");
        }

        List<RagEvalCase> cases;
        await using (var stream = System.IO.File.OpenRead(path))
        {
            cases = RagEvaluationService.LoadCases(stream);
        }

        var report = await evaluation.RunAsync(cases, generateAnswers, HttpContext.RequestAborted);
        return Success(report);
    }

    [HttpGet("documents")]
    public async Task<IActionResult> List() =>
        Success((await _documents.ListAsync(HttpContext.RequestAborted)).Select(ToStatus));

    [HttpGet("documents/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id)
    {
        var document = await _documents.GetByIdAsync(id, HttpContext.RequestAborted);
        return document == null
            ? Failure(StatusCodes.Status404NotFound, "Document not found.")
            : Success(ToStatus(document));
    }

    private static object ToStatus(KnowledgeDocument d) => new
    {
        d.Id,
        d.Title,
        d.DocumentType,
        d.SourceName,
        d.SourceUrl,
        d.TaxYear,
        d.Version,
        d.Status,
        d.LastError,
        d.PageCount,
        d.ChunkCount,
        d.ContentHash,
        d.CreatedAt,
        d.IndexedAt
    };
}
