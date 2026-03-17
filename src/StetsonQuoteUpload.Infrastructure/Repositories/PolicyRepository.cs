using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<PolicyRepository> _logger;

    public PolicyRepository(AppDbContext db, ILogger<PolicyRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task MergeRangeAsync(IEnumerable<Policy> policies, CancellationToken ct = default)
    {
        // Partial-success: each policy is merged individually so one failure doesn't abort others
        foreach (var policy in policies)
        {
            try
            {
                var existing = await _db.Policies
                    .FirstOrDefaultAsync(p => p.PolicyNumber == policy.PolicyNumber, ct);

                if (existing == null)
                {
                    _db.Policies.Add(policy);
                }
                else
                {
                    existing.Name = policy.Name;
                    existing.QuoteId = policy.QuoteId;
                    existing.CoverageCode = policy.CoverageCode;
                    existing.EffectiveDate = policy.EffectiveDate;
                    existing.PolicyTerm = policy.PolicyTerm;
                    existing.CarrierCode = policy.CarrierCode;
                    existing.GACode = policy.GACode;
                    existing.Premium = policy.Premium;
                    existing.MinimumEarnedPercentage = policy.MinimumEarnedPercentage;
                    existing.IsAuditable = policy.IsAuditable;
                    existing.LastModifiedDate = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge policy {PolicyNumber}", policy.PolicyNumber);
            }
        }
    }
}
