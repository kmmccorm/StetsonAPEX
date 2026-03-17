using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.FinancePro;
using StetsonQuoteUpload.Core.Interfaces;

namespace StetsonQuoteUpload.Infrastructure.FinancePro;

/// <summary>
/// Hand-coded SOAP client for the FinancePro QuoteService.asmx.
/// WSDL namespace: http://www.financepro.com/financepro/WebServices/
/// </summary>
public class FinanceProSoapClient : IFinanceProClient
{
    private readonly HttpClient _http;
    private readonly FinanceProCredentials _credentials;
    private readonly ILogger<FinanceProSoapClient> _logger;

    private const string SoapNs = "http://www.financepro.com/financepro/WebServices/";

    public FinanceProSoapClient(
        HttpClient http,
        FinanceProCredentials credentials,
        ILogger<FinanceProSoapClient> logger)
    {
        _http = http;
        _credentials = credentials;
        _logger = logger;
    }

    public async Task<FpResults> GetQuoteByIdAsync(int quoteId, CancellationToken ct = default)
    {
        _logger.LogDebug("FinancePro GetQuoteByID({QuoteId})", quoteId);

        var soapAction = $"{SoapNs}GetQuoteByID";
        var body = $@"<GetQuoteByID xmlns=""{SoapNs}""><quoteID>{quoteId}</quoteID></GetQuoteByID>";
        var responseXml = await SendSoapRequestAsync(soapAction, body, ct);

        return ParseGetQuoteByIdResponse(responseXml);
    }

    public async Task<string> GetQuoteAgreementUrlAsync(int quoteId, CancellationToken ct = default)
    {
        _logger.LogDebug("FinancePro GetQuoteAgreementURL({QuoteId})", quoteId);

        var soapAction = $"{SoapNs}GetQuoteAgreementURL";
        var body = $@"<GetQuoteAgreementURL xmlns=""{SoapNs}""><quoteID>{quoteId}</quoteID></GetQuoteAgreementURL>";
        var responseXml = await SendSoapRequestAsync(soapAction, body, ct);

        return ParseGetQuoteAgreementUrlResponse(responseXml);
    }

    private async Task<string> SendSoapRequestAsync(string soapAction, string bodyContent, CancellationToken ct)
    {
        var envelope = BuildSoapEnvelope(bodyContent);

        using var request = new HttpRequestMessage(HttpMethod.Post, _credentials.EndpointUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", $"\"{soapAction}\"");

        var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("FinancePro SOAP call failed: {Status} {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"FinancePro SOAP call failed with {response.StatusCode}");
        }

        return responseBody;
    }

    private string BuildSoapEnvelope(string bodyContent)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <soap:Header>
    <AuthHeader xmlns=""{SoapNs}"">
      <Key>{SecurityElement(_credentials.ImporterKey)}</Key>
      <Login>
        <LoginName>{SecurityElement(_credentials.Username)}</LoginName>
        <LoginPassword>{SecurityElement(_credentials.Password)}</LoginPassword>
        <FirstName>First</FirstName>
        <LastName>Last</LastName>
        <Phone>Mobile</Phone>
        <Email>fake@email.test</Email>
        <UserID>0</UserID>
      </Login>
      <SiteID>0</SiteID>
      <LogImport>false</LogImport>
    </AuthHeader>
  </soap:Header>
  <soap:Body>
    {bodyContent}
  </soap:Body>
</soap:Envelope>";
    }

    private static string SecurityElement(string? value)
        => System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;

    private static FpResults ParseGetQuoteByIdResponse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("fp", SoapNs);
        ns.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");

        var resultNode = doc.SelectSingleNode("//fp:GetQuoteByIDResult", ns);

        var results = new FpResults
        {
            Errors = new FpErrors()
        };

        if (resultNode == null) return results;

        // Parse errors
        var errorNodes = resultNode.SelectNodes(".//fp:Error", ns);
        if (errorNodes != null && errorNodes.Count > 0)
        {
            results.Errors.Error = new List<FpError>();
            foreach (XmlNode errNode in errorNodes)
            {
                results.Errors.Error.Add(new FpError
                {
                    ErrorCode = errNode.SelectSingleNode("fp:ErrorCode", ns)?.InnerText,
                    Description = errNode.SelectSingleNode("fp:Description", ns)?.InnerText,
                    Message = errNode.SelectSingleNode("fp:Message", ns)?.InnerText,
                    Severity = errNode.SelectSingleNode("fp:Severity", ns)?.InnerText,
                    PolicyID = TryParseInt(errNode.SelectSingleNode("fp:PolicyID", ns)?.InnerText)
                });
            }
            return results;
        }

        // Parse Quote
        var quoteNode = resultNode.SelectSingleNode("fp:Quote", ns);
        if (quoteNode == null) return results;

        results.Quote = new FpQuote
        {
            QuoteNumber = quoteNode.SelectSingleNode("fp:QuoteNumber", ns)?.InnerText,
            EffectiveDate = TryParseDateTime(quoteNode.SelectSingleNode("fp:EffectiveDate", ns)?.InnerText),
            NumberOfPayments = TryParseInt(quoteNode.SelectSingleNode("fp:NumberOfPayments", ns)?.InnerText) ?? 0,
            PaymentAmount = TryParseDecimal(quoteNode.SelectSingleNode("fp:PaymentAmount", ns)?.InnerText),
            DownPayment = TryParseDecimal(quoteNode.SelectSingleNode("fp:DownPayment", ns)?.InnerText),
            AmountFinanced = TryParseDecimal(quoteNode.SelectSingleNode("fp:AmountFinanced", ns)?.InnerText),
            InterestRate = TryParseDecimal(quoteNode.SelectSingleNode("fp:InterestRate", ns)?.InnerText),
            FinanceCharge = TryParseDecimal(quoteNode.SelectSingleNode("fp:FinanceCharge", ns)?.InnerText),
            AgentCompensation = TryParseDecimal(quoteNode.SelectSingleNode("fp:AgentCompensation", ns)?.InnerText),
            DocStampFee = TryParseDecimal(quoteNode.SelectSingleNode("fp:DocStampFee", ns)?.InnerText),
            FirstPaymentDue = TryParseDateTime(quoteNode.SelectSingleNode("fp:FirstPaymentDue", ns)?.InnerText),
            TotalGrossPremium = TryParseDecimal(quoteNode.SelectSingleNode("fp:TotalGrossPremium", ns)?.InnerText)
        };

        var insuredNode = quoteNode.SelectSingleNode("fp:Insured", ns);
        if (insuredNode != null)
        {
            results.Quote.Insured = new FpInsured
            {
                Name = insuredNode.SelectSingleNode("fp:Name", ns)?.InnerText,
                Phone = insuredNode.SelectSingleNode("fp:Phone", ns)?.InnerText,
                Email = insuredNode.SelectSingleNode("fp:Email", ns)?.InnerText,
                Address1 = insuredNode.SelectSingleNode("fp:Address1", ns)?.InnerText,
                City = insuredNode.SelectSingleNode("fp:City", ns)?.InnerText,
                State = insuredNode.SelectSingleNode("fp:State", ns)?.InnerText,
                Zip = insuredNode.SelectSingleNode("fp:Zip", ns)?.InnerText,
                Country = insuredNode.SelectSingleNode("fp:Country", ns)?.InnerText
            };
        }

        var agencyNode = quoteNode.SelectSingleNode("fp:Agency", ns);
        if (agencyNode != null)
        {
            results.Quote.Agency = new FpAgency
            {
                ContactName = agencyNode.SelectSingleNode("fp:ContactName", ns)?.InnerText,
                SearchCode = agencyNode.SelectSingleNode("fp:SearchCode", ns)?.InnerText
            };
        }

        var policyNodes = quoteNode.SelectNodes(".//fp:Policies/fp:Policy", ns);
        if (policyNodes != null && policyNodes.Count > 0)
        {
            results.Quote.Policies = new FpPolicies { Policy = new List<FpPolicy>() };
            foreach (XmlNode polNode in policyNodes)
            {
                results.Quote.Policies.Policy.Add(new FpPolicy
                {
                    PolicyNumber = polNode.SelectSingleNode("fp:PolicyNumber", ns)?.InnerText,
                    CoverageType = polNode.SelectSingleNode("fp:CoverageType", ns)?.InnerText,
                    InceptionDate = TryParseDateTime(polNode.SelectSingleNode("fp:InceptionDate", ns)?.InnerText),
                    PolicyTerm = TryParseNullableDecimal(polNode.SelectSingleNode("fp:PolicyTerm", ns)?.InnerText),
                    InsuranceCompany = ParseCompany(polNode.SelectSingleNode("fp:InsuranceCompany", ns), ns),
                    GeneralAgency = ParseCompany(polNode.SelectSingleNode("fp:GeneralAgency", ns), ns),
                    GrossPremium = TryParseDecimal(polNode.SelectSingleNode("fp:GrossPremium", ns)?.InnerText),
                    MinEarnedPercent = TryParseDecimal(polNode.SelectSingleNode("fp:MinEarnedPercent", ns)?.InnerText),
                    Auditable = string.Equals(polNode.SelectSingleNode("fp:Auditable", ns)?.InnerText, "true", StringComparison.OrdinalIgnoreCase),
                    PolicyFee = TryParseDecimal(polNode.SelectSingleNode("fp:PolicyFee", ns)?.InnerText),
                    BrokerFee = TryParseDecimal(polNode.SelectSingleNode("fp:BrokerFee", ns)?.InnerText),
                    InspectionFee = TryParseDecimal(polNode.SelectSingleNode("fp:InspectionFee", ns)?.InnerText),
                    TaxStampFee = TryParseDecimal(polNode.SelectSingleNode("fp:TaxStampFee", ns)?.InnerText),
                    LastModifiedDate = TryParseDateTime(polNode.SelectSingleNode("fp:LastModifiedDate", ns)?.InnerText),
                    DateEntered = TryParseDateTime(polNode.SelectSingleNode("fp:DateEntered", ns)?.InnerText),
                    EnteredByUserName = polNode.SelectSingleNode("fp:EnteredByUserName", ns)?.InnerText
                });
            }
        }

        return results;
    }

    private static string ParseGetQuoteAgreementUrlResponse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("fp", SoapNs);

        return doc.SelectSingleNode("//fp:GetQuoteAgreementURLResult", ns)?.InnerText ?? string.Empty;
    }

    private static FpCompany? ParseCompany(XmlNode? node, XmlNamespaceManager ns)
    {
        if (node == null) return null;
        return new FpCompany { SearchCode = node.SelectSingleNode("fp:SearchCode", ns)?.InnerText };
    }

    private static int? TryParseInt(string? s)
        => int.TryParse(s, out var v) ? v : null;

    private static decimal TryParseDecimal(string? s)
        => decimal.TryParse(s, out var v) ? v : 0m;

    private static decimal? TryParseNullableDecimal(string? s)
        => decimal.TryParse(s, out var v) ? v : null;

    private static DateTime? TryParseDateTime(string? s)
        => DateTime.TryParse(s, out var v) ? v : null;
}
