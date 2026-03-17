namespace StetsonQuoteUpload.Core.Models;

public class Policy
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote? Quote { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? CoverageCode { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public decimal? PolicyTerm { get; set; }
    public string? CarrierCode { get; set; }
    public string? GACode { get; set; }
    public decimal? Premium { get; set; }
    public decimal? MinimumEarnedPercentage { get; set; }
    public bool? IsAuditable { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }
}
