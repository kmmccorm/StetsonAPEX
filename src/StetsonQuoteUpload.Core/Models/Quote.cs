namespace StetsonQuoteUpload.Core.Models;

public class Quote
{
    public int Id { get; set; }
    public string QuoteId { get; set; } = string.Empty;
    public string? QuoteNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public string? AGTNumber { get; set; }
    public string? QuotingForCode { get; set; }
    public string? QuotingForAltCode { get; set; }
    public string APIPartnerID { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string? QuoteStatus { get; set; }
    public string? QuoteApprover { get; set; }
    public string? FirstNamedInsured { get; set; }
    public string? InsuredInReceivership { get; set; }
    public string? RetailAgentContactName { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly CloseDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingPostalCode { get; set; }
    public string? MailingCountry { get; set; }
    public string? PhysicalStreet { get; set; }
    public string? PhysicalCity { get; set; }
    public string? PhysicalState { get; set; }
    public string? PhysicalPostalCode { get; set; }
    public string? PhysicalCountry { get; set; }
    public decimal? TotalPaymentAmount { get; set; }
    public int? NumberOfInstallments { get; set; }
    public decimal? DownPayment { get; set; }
    public decimal? InstallmentAmount { get; set; }
    public decimal? AmountFinanced { get; set; }
    public decimal? APR { get; set; }
    public decimal? FinanceCharge { get; set; }
    public decimal? AgentReferralFeeAmount { get; set; }
    public decimal? DocStampTax { get; set; }
    public decimal? QuotingForReferralFeeAmount { get; set; }
    public string? AgentCode { get; set; }
    public int PolicyCount { get; set; }
    public DateOnly? FirstInstallmentDueDate { get; set; }
    public string? FinanceAgreementPdfUrl { get; set; }
    public decimal? TotalPremium { get; set; }
    public decimal? NonRefundableFee { get; set; }
    public DateOnly? QuoteLastModifiedDate { get; set; }
    public DateOnly? QuoteCreateDate { get; set; }
    public string? QuoteCreateUser { get; set; }
    public string? RyanCompanyName { get; set; }
    public bool IntegrationError { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorDescription { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorSeverity { get; set; }
    public int? ErrorPolicyId { get; set; }
    public Guid? JobId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastModifiedDate { get; set; }

    public ICollection<Policy> Policies { get; set; } = new List<Policy>();
}
