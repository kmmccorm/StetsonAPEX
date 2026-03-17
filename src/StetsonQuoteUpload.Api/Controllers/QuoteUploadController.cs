using Hangfire;
using Microsoft.AspNetCore.Mvc;
using StetsonQuoteUpload.Api.Jobs;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Services;

namespace StetsonQuoteUpload.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public class QuoteUploadController : ControllerBase
{
    private readonly QuoteIntakeService _intakeService;
    private readonly IJobTrackingRepository _jobRepo;
    private readonly IBackgroundJobClient _hangfire;
    private readonly ILogger<QuoteUploadController> _logger;

    public QuoteUploadController(
        QuoteIntakeService intakeService,
        IJobTrackingRepository jobRepo,
        IBackgroundJobClient hangfire,
        ILogger<QuoteUploadController> logger)
    {
        _intakeService = intakeService;
        _jobRepo = jobRepo;
        _hangfire = hangfire;
        _logger = logger;
    }

    /// <summary>POST api/quotes/upload — Step 1: intake quotes and kick off enrichment batch.</summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOpportunities(
        [FromBody] CreateOpportunitiesRequest request,
        CancellationToken ct)
    {
        if (request.QuoteIds == null || request.QuoteIds.Count == 0)
            return BadRequest("quoteIds must not be empty.");

        // Validate: all keys must be numeric strings
        foreach (var key in request.QuoteIds.Keys)
        {
            if (!long.TryParse(key, out _))
                return BadRequest($"QuoteId '{key}' is not a valid numeric string.");
        }

        if (!new[] { "USPF", "AFCO" }.Contains(request.Source, StringComparer.OrdinalIgnoreCase))
            return BadRequest("source must be 'USPF' or 'AFCO'.");

        _logger.LogInformation("Upload request: {Count} quotes, source={Source}", request.QuoteIds.Count, request.Source);

        var jobId = await _intakeService.CreateOpportunitiesAsync(request.QuoteIds, request.Source, ct);

        // Enqueue the enrichment job in Hangfire
        _hangfire.Enqueue<QuoteEnrichmentJob>(job => job.ExecuteAsync(jobId, CancellationToken.None));

        return Ok(new UploadResponse { JobId = jobId });
    }

    /// <summary>GET api/quotes/template — returns the template CSV download URL.</summary>
    [HttpGet("template")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplateDoc(CancellationToken ct)
    {
        var url = await _intakeService.GetTemplateDocUrlAsync(ct);
        if (url == null) return NotFound("Template file not configured.");
        return Ok(new TemplateResponse { DownloadUrl = url });
    }

    /// <summary>GET api/quotes/jobs/{jobId} — poll status of an enrichment job.</summary>
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobDetails(Guid jobId, CancellationToken ct)
    {
        var job = await _jobRepo.GetByIdAsync(jobId, ct);
        if (job == null) return NotFound();

        return Ok(new JobStatusResponse
        {
            JobId = job.Id,
            Status = job.Status,
            TotalItems = job.TotalItems,
            ItemsProcessed = job.ItemsProcessed,
            NumberOfErrors = job.NumberOfErrors,
            Source = job.Source,
            CreatedDate = job.CreatedDate,
            CompletedDate = job.CompletedDate
        });
    }
}

public record CreateOpportunitiesRequest(
    Dictionary<string, string> QuoteIds,
    string Source);

public record UploadResponse(Guid JobId);

public record TemplateResponse(string DownloadUrl);

public record JobStatusResponse
{
    public Guid JobId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalItems { get; init; }
    public int ItemsProcessed { get; init; }
    public int NumberOfErrors { get; init; }
    public string? Source { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime? CompletedDate { get; init; }
}
