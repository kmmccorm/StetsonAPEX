using Hangfire;
using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.Services;

namespace StetsonQuoteUpload.Api.Jobs;

/// <summary>
/// Hangfire job that processes quote enrichment for a given job ID.
/// Enqueued by the Quote Intake endpoint after placeholder quotes are created.
/// </summary>
public class QuoteEnrichmentJob
{
    private readonly QuoteEnrichmentService _enrichmentService;
    private readonly ILogger<QuoteEnrichmentJob> _logger;

    public QuoteEnrichmentJob(
        QuoteEnrichmentService enrichmentService,
        ILogger<QuoteEnrichmentJob> logger)
    {
        _enrichmentService = enrichmentService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)] // Retries managed within the service with Polly
    public async Task ExecuteAsync(Guid jobId, CancellationToken ct)
    {
        _logger.LogInformation("Hangfire: starting QuoteEnrichmentJob for job {JobId}", jobId);
        try
        {
            await _enrichmentService.ProcessJobAsync(jobId, ct);
            _logger.LogInformation("Hangfire: QuoteEnrichmentJob {JobId} completed", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangfire: QuoteEnrichmentJob {JobId} failed", jobId);
            throw;
        }
    }
}
