using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.FinancePro;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Services;

public class QuoteEnrichmentService
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IPolicyRepository _policyRepo;
    private readonly IJobTrackingRepository _jobRepo;
    private readonly ILenderConfigRepository _lenderRepo;
    private readonly IFinanceProClientFactory _clientFactory;
    private readonly IAppSettingRepository _settings;
    private readonly IPubSubPublisher _pubSubPublisher;
    private readonly ILogger<QuoteEnrichmentService> _logger;

    public QuoteEnrichmentService(
        IQuoteRepository quoteRepo,
        IPolicyRepository policyRepo,
        IJobTrackingRepository jobRepo,
        ILenderConfigRepository lenderRepo,
        IFinanceProClientFactory clientFactory,
        IAppSettingRepository settings,
        IPubSubPublisher pubSubPublisher,
        ILogger<QuoteEnrichmentService> logger)
    {
        _quoteRepo = quoteRepo;
        _policyRepo = policyRepo;
        _jobRepo = jobRepo;
        _lenderRepo = lenderRepo;
        _clientFactory = clientFactory;
        _settings = settings;
        _pubSubPublisher = pubSubPublisher;
        _logger = logger;
    }

    public async Task ProcessJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepo.GetByIdAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Job {jobId} not found");

        var source = job.Source ?? "USPF";
        var lenderConfig = await _lenderRepo.GetByNameAsync(source, ct)
            ?? throw new InvalidOperationException($"No LenderConfiguration found for source '{source}'");

        await _jobRepo.UpdateStatusAsync(jobId, JobStatus.Processing, 0, 0, ct);

        var batchSize = await _settings.GetIntValueAsync(AppSettingKeys.QuoteUploadBatchSize, 50, ct);
        batchSize = Math.Min(batchSize, 50);

        var client = await _clientFactory.CreateAsync(lenderConfig, ct);
        var allQuotes = await _quoteRepo.GetQuotesForJobAsync(jobId, ct);
        var pubSubMessages = new List<USPFPubSubMessage>();
        var failuresMap = new Dictionary<int, (string Code, string Description, string Message)>();

        int processed = 0;
        int errors = 0;

        foreach (var batch in allQuotes.Chunk(batchSize))
        {
            var policiesToMerge = new List<Policy>();

            foreach (var quote in batch)
            {
                try
                {
                    _logger.LogInformation("Enriching quote {QuoteId} (job {JobId})", quote.QuoteId, jobId);
                    var results = await client.GetQuoteByIdAsync(int.Parse(quote.QuoteId), ct);
                    var agreementUrl = await client.GetQuoteAgreementUrlAsync(int.Parse(quote.QuoteId), ct);

                    if (results.Errors?.Error != null && results.Errors.Error.Count > 0)
                    {
                        var err = results.Errors.Error[0];
                        _logger.LogWarning("FinancePro API error for quote {QuoteId}: {Code} {Msg}", quote.QuoteId, err.ErrorCode, err.Message);
                        ApplyApiError(quote, err);
                        errors++;
                    }
                    else if (results.Quote != null)
                    {
                        var fpQuote = results.Quote;
                        MapQuoteFields(quote, fpQuote, agreementUrl, source);

                        if (fpQuote.Policies?.Policy != null && fpQuote.Policies.Policy.Count > 0)
                        {
                            var policies = MapPolicies(fpQuote, quote.Id);
                            ApplyPolicyAggregations(quote, fpQuote);
                            policiesToMerge.AddRange(policies);
                        }

                        var message = BuildPubSubMessage(quote, fpQuote, source);
                        pubSubMessages.Add(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception processing quote {QuoteId}", quote.QuoteId);
                    ApplyExceptionError(quote, ex);
                    errors++;
                }

                processed++;
            }

            // Partial-success update for the batch
            foreach (var quote in batch)
            {
                try { await _quoteRepo.UpdateAsync(quote, ct); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save quote {QuoteId}", quote.QuoteId);
                    failuresMap[quote.Id] = (ex.GetType().Name, ex.StackTrace ?? string.Empty, ex.Message);
                }
            }

            if (policiesToMerge.Count > 0)
                await _policyRepo.MergeRangeAsync(policiesToMerge, ct);

            await _jobRepo.UpdateStatusAsync(jobId, JobStatus.Processing, processed, errors, ct);
        }

        // Write back save failures
        foreach (var (quoteDbId, failure) in failuresMap)
            await _quoteRepo.UpdateErrorAsync(quoteDbId, failure.Code, failure.Description, failure.Message, "Fatal", ct);

        await _jobRepo.CompleteAsync(jobId, JobStatus.Completed, ct);
        _logger.LogInformation("Job {JobId} complete. Processed={Processed}, Errors={Errors}", jobId, processed, errors);

        if (pubSubMessages.Count > 0)
        {
            _logger.LogInformation("Publishing {Count} PubSub messages", pubSubMessages.Count);
            await _pubSubPublisher.PublishAsync(pubSubMessages, ct);
        }
    }

    private static void MapQuoteFields(Quote quote, FpQuote fp, string agreementUrl, string source)
    {
        quote.Name = $"{fp.QuoteNumber} {fp.Insured?.Name}";
        quote.QuoteNumber = fp.QuoteNumber;
        quote.StageName = "Quote Released to RT/Agent";
        quote.QuoteStatus = "New Quote";
        quote.APIPartnerID = source;
        quote.QuoteApprover = "Ryan Integration";
        quote.FirstNamedInsured = fp.Insured?.Name;
        quote.InsuredInReceivership = "No";
        quote.RetailAgentContactName = fp.Agency?.ContactName;
        quote.EffectiveDate = fp.EffectiveDate.HasValue ? DateOnly.FromDateTime(fp.EffectiveDate.Value) : null;
        quote.CloseDate = fp.EffectiveDate.HasValue ? DateOnly.FromDateTime(fp.EffectiveDate.Value) : quote.CloseDate;
        quote.PhoneNumber = fp.Insured?.Phone;
        quote.EmailAddress = fp.Insured?.Email;
        quote.MailingStreet = fp.Insured?.Address1;
        quote.MailingCity = fp.Insured?.City;
        quote.MailingState = fp.Insured?.State;
        quote.MailingPostalCode = fp.Insured?.Zip;
        quote.MailingCountry = fp.Insured?.Country;
        quote.PhysicalStreet = fp.Insured?.Address1;
        quote.PhysicalCity = fp.Insured?.City;
        quote.PhysicalState = fp.Insured?.State;
        quote.PhysicalPostalCode = fp.Insured?.Zip;
        quote.PhysicalCountry = fp.Insured?.Country;
        quote.TotalPaymentAmount = fp.NumberOfPayments * fp.PaymentAmount;
        quote.NumberOfInstallments = fp.NumberOfPayments;
        quote.DownPayment = fp.DownPayment;
        quote.InstallmentAmount = fp.PaymentAmount;
        quote.AmountFinanced = fp.AmountFinanced;
        quote.APR = fp.InterestRate;
        quote.FinanceCharge = fp.FinanceCharge;
        quote.AgentReferralFeeAmount = fp.AgentCompensation;
        quote.DocStampTax = fp.DocStampFee;
        quote.QuotingForReferralFeeAmount = fp.AgentCompensation;
        quote.AgentCode = fp.Agency?.SearchCode;
        quote.PolicyCount = 0;
        quote.FirstInstallmentDueDate = fp.FirstPaymentDue.HasValue ? DateOnly.FromDateTime(fp.FirstPaymentDue.Value) : null;
        quote.FinanceAgreementPdfUrl = agreementUrl;
        quote.IntegrationError = false;
        quote.ErrorCode = string.Empty;
        quote.ErrorDescription = string.Empty;
        quote.ErrorMessage = string.Empty;
        quote.ErrorSeverity = string.Empty;
        quote.ErrorPolicyId = null;
        quote.LastModifiedDate = DateTime.UtcNow;
    }

    private static List<Policy> MapPolicies(FpQuote fp, int quoteDbId)
    {
        if (fp.Policies?.Policy == null) return new List<Policy>();

        return fp.Policies.Policy.Select(pol => new Policy
        {
            QuoteId = quoteDbId,
            PolicyNumber = pol.PolicyNumber ?? string.Empty,
            Name = pol.PolicyNumber,
            CoverageCode = pol.CoverageType,
            EffectiveDate = pol.InceptionDate.HasValue ? DateOnly.FromDateTime(pol.InceptionDate.Value) : null,
            PolicyTerm = pol.PolicyTerm,
            CarrierCode = pol.InsuranceCompany?.SearchCode,
            GACode = pol.GeneralAgency?.SearchCode,
            Premium = pol.GrossPremium,
            MinimumEarnedPercentage = pol.MinEarnedPercent,
            IsAuditable = pol.Auditable,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        }).ToList();
    }

    private static void ApplyPolicyAggregations(Quote quote, FpQuote fp)
    {
        var policies = fp.Policies!.Policy!;
        quote.RyanCompanyName = policies[0].EnteredByUserName;
        quote.QuoteCreateUser = policies[0].EnteredByUserName;
        quote.QuoteLastModifiedDate = policies
            .Where(p => p.LastModifiedDate.HasValue)
            .Select(p => DateOnly.FromDateTime(p.LastModifiedDate!.Value))
            .DefaultIfEmpty()
            .Max();
        quote.QuoteCreateDate = policies
            .Where(p => p.DateEntered.HasValue)
            .Select(p => DateOnly.FromDateTime(p.DateEntered!.Value))
            .DefaultIfEmpty()
            .Min();
        quote.NonRefundableFee = policies.Sum(p => p.PolicyFee + p.BrokerFee + p.InspectionFee);
        quote.TotalPremium = fp.TotalGrossPremium + policies.Sum(p => p.PolicyFee + p.BrokerFee + p.InspectionFee + p.TaxStampFee);
        quote.PolicyCount = policies.Count;
    }

    private static void ApplyApiError(Quote quote, FpError err)
    {
        quote.IntegrationError = true;
        quote.ErrorCode = err.ErrorCode;
        quote.ErrorDescription = err.Description;
        quote.ErrorMessage = err.Message;
        quote.ErrorSeverity = err.Severity;
        quote.ErrorPolicyId = err.PolicyID;
        quote.LastModifiedDate = DateTime.UtcNow;
    }

    private static void ApplyExceptionError(Quote quote, Exception ex)
    {
        quote.IntegrationError = true;
        quote.ErrorCode = ex.GetType().Name;
        quote.ErrorDescription = ex.StackTrace;
        quote.ErrorMessage = ex.Message;
        quote.ErrorSeverity = "Fatal";
        quote.LastModifiedDate = DateTime.UtcNow;
    }

    private static USPFPubSubMessage BuildPubSubMessage(Quote quote, FpQuote fp, string source)
    {
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ss.ff");
        var effectiveDateStr = fp.EffectiveDate.HasValue
            ? DateOnly.FromDateTime(fp.EffectiveDate.Value).ToString("yyyy-MM-dd")
            : string.Empty;

        return new USPFPubSubMessage
        {
            SourceSystemId = quote.QuoteNumber ?? string.Empty,
            DocumentOwnerId = quote.QuoteNumber ?? string.Empty,
            DocumentDescription = fp.Insured?.Name ?? string.Empty,
            FileName = $"{quote.QuoteNumber}_PFA.pdf",
            CreatedAt = now,
            LastModifiedAt = now,
            ReceivedAt = now,
            SentOn = now,
            Versions = new List<PubSubVersion>
            {
                new() { Url = quote.FinanceAgreementPdfUrl ?? string.Empty }
            },
            ImageRightFileAttributes = new List<PubSubFileAttribute>
            {
                new() { Name = "Lender",          Type = "string",   Value = source },
                new() { Name = "AGT_Code",        Type = "string",   Value = quote.AGTNumber ?? string.Empty },
                new() { Name = "Effective",       Type = "datetime", Value = effectiveDateStr },
                new() { Name = "Expiration",      Type = "datetime", Value = effectiveDateStr },
                new() { Name = "Amount_Financed", Type = "string",   Value = fp.AmountFinanced.ToString() },
                new() { Name = "AGENT",           Type = "string",   Value = fp.Agency?.SearchCode ?? string.Empty }
            }
        };
    }
}
