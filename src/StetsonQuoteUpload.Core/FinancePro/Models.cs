namespace StetsonQuoteUpload.Core.FinancePro;

public class FpResults
{
    public FpQuote? Quote { get; set; }
    public FpErrors? Errors { get; set; }
}

public class FpErrors
{
    public List<FpError>? Error { get; set; }
}

public class FpError
{
    public string? ErrorCode { get; set; }
    public string? Description { get; set; }
    public string? Message { get; set; }
    public string? Severity { get; set; }
    public int? PolicyID { get; set; }
}

public class FpQuote
{
    public string? QuoteNumber { get; set; }
    public FpInsured? Insured { get; set; }
    public FpAgency? Agency { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int NumberOfPayments { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal AmountFinanced { get; set; }
    public decimal InterestRate { get; set; }
    public decimal FinanceCharge { get; set; }
    public decimal AgentCompensation { get; set; }
    public decimal DocStampFee { get; set; }
    public DateTime? FirstPaymentDue { get; set; }
    public decimal TotalGrossPremium { get; set; }
    public FpPolicies? Policies { get; set; }
}

public class FpInsured
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
}

public class FpAgency
{
    public string? ContactName { get; set; }
    public string? SearchCode { get; set; }
}

public class FpPolicies
{
    public List<FpPolicy>? Policy { get; set; }
}

public class FpPolicy
{
    public string? PolicyNumber { get; set; }
    public string? CoverageType { get; set; }
    public DateTime? InceptionDate { get; set; }
    public decimal? PolicyTerm { get; set; }
    public FpCompany? InsuranceCompany { get; set; }
    public FpCompany? GeneralAgency { get; set; }
    public decimal GrossPremium { get; set; }
    public decimal MinEarnedPercent { get; set; }
    public bool Auditable { get; set; }
    public decimal PolicyFee { get; set; }
    public decimal BrokerFee { get; set; }
    public decimal InspectionFee { get; set; }
    public decimal TaxStampFee { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public DateTime? DateEntered { get; set; }
    public string? EnteredByUserName { get; set; }
}

public class FpCompany
{
    public string? SearchCode { get; set; }
}
