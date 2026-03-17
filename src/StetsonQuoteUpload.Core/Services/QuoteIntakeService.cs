using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Services;

public class QuoteIntakeService
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IAccountRepository _accountRepo;
    private readonly IAppSettingRepository _settings;
    private readonly IJobTrackingRepository _jobRepo;
    private readonly ILogger<QuoteIntakeService> _logger;

    public QuoteIntakeService(
        IQuoteRepository quoteRepo,
        IAccountRepository accountRepo,
        IAppSettingRepository settings,
        IJobTrackingRepository jobRepo,
        ILogger<QuoteIntakeService> logger)
    {
        _quoteRepo = quoteRepo;
        _accountRepo = accountRepo;
        _settings = settings;
        _jobRepo = jobRepo;
        _logger = logger;
    }

    public async Task<Guid> CreateOpportunitiesAsync(
        Dictionary<string, string> quoteIds,
        string source,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating {Count} placeholder quotes for source {Source}", quoteIds.Count, source);

        var placeholderAgtCode = await _settings.GetValueAsync(AppSettingKeys.PlaceholderAGTCode, ct)
            ?? throw new InvalidOperationException("PlaceholderAGTCode not configured");

        var fallbackAccount = await _accountRepo.GetByAGTCodeAsync(placeholderAgtCode, ct)
            ?? throw new InvalidOperationException($"No fallback Account found for AGTCode '{placeholderAgtCode}'");

        var matchingAccounts = await _accountRepo.GetIdsByAGTCodesAsync(quoteIds.Values.Distinct(), ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var apiPartnerId = source.Equals("AFCO", StringComparison.OrdinalIgnoreCase) ? "AFCO" : "USPF";

        var quotes = quoteIds.Select(kvp =>
        {
            var (quoteId, agtCode) = kvp;
            matchingAccounts.TryGetValue(agtCode, out var accountId);
            if (accountId == 0) accountId = fallbackAccount.Id;

            return new Quote
            {
                QuoteId = quoteId,
                QuoteNumber = quoteId,
                Name = $"Placeholder Quote {quoteId}",
                StageName = "Quote Released to RT/Agent",
                AGTNumber = agtCode,
                QuotingForCode = agtCode,
                QuotingForAltCode = agtCode,
                AccountId = accountId,
                CloseDate = today.AddDays(60),
                APIPartnerID = apiPartnerId,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }).ToList();

        await _quoteRepo.UpsertRangeAsync(quotes, ct);

        var batchSize = await _settings.GetIntValueAsync(AppSettingKeys.QuoteUploadBatchSize, 50, ct);
        batchSize = Math.Min(batchSize, 50);

        var job = new JobTracking
        {
            Status = JobStatus.Queued,
            TotalItems = quotes.Count,
            Source = apiPartnerId,
            CreatedDate = DateTime.UtcNow
        };
        var jobId = await _jobRepo.CreateAsync(job, ct);

        // Associate quotes with this job for the enrichment step to query
        var quoteDbIds = quotes.Where(q => q.Id > 0).Select(q => q.Id);
        await _quoteRepo.AssociateWithJobAsync(quoteDbIds, jobId, ct);

        _logger.LogInformation("Created job {JobId} for {Count} quotes (batch size {BatchSize})", jobId, quotes.Count, batchSize);
        return jobId;
    }

    public async Task<string?> GetTemplateDocUrlAsync(CancellationToken ct = default)
    {
        var fileName = await _settings.GetValueAsync(AppSettingKeys.TemplateFileName, ct);
        return fileName;
    }
}
