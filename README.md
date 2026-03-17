#StesonAPEX

Pipeline 2: USPF/AFCO Quote Upload

.NET Rewrite Requirements — Stetson Insurance

1. End-to-End Flow Diagram

User (UI) uploads CSV of {QuoteID → AGT_Code} pairs + selects source (USPF|AFCO)
Step 1: Quote Intake Controller (REST API) — createOpportunities(): Resolve AGT_Code → Account, create placeholder Quotes, kick off batch processing.

Step 2: Quote Enrichment Batch (Background job processor) — For each quote: call FinancePro GetQuoteByID, call FinancePro GetQuoteAgreementURL, map response → Quote fields, map policies → Policy records, build PubSub message, persist to SQL Server. On finish: write back save failures, enqueue PubSub job.

Step 3: GCP Pub/Sub Publisher (Async queue job) — Authenticate to GCP via JWT, POST messages to Pub/Sub topic, batch in chunks of 1000.

3. Step 1: Quote Intake & Placeholder Creation
2.1 Functional Description
A user submits a set of quote IDs with associated AGT (agent) codes and a lender source identifier. The system creates initial placeholder quote records in SQL Server before kicking off asynchronous enrichment.
2.2 Detailed Behavior
	∙	Input: A map of {QuoteID: AGTCode} pairs (QuoteID is numeric string, AGTCode is string), plus source string (“USPF” or “AFCO”).
	∙	Account Resolution: Fetch a generic/fallback Account by configurable AGT code (stored in AppSetting table, key PlaceholderAGTCode). Bulk-query all Accounts whose AGTCode matches any submitted AGT code value. For each quote: if a matching Account exists, link to it; otherwise link to the generic fallback Account.
	∙	Placeholder Quote Creation:
	∙	Name = “Placeholder Quote {QuoteID}”
	∙	StageName = “Quote Released to RT/Agent”
	∙	AGTNumber, QuotingForCode, QuotingForAltCode = the AGT code
	∙	QuoteId and QuoteNumber = the Quote ID
	∙	CloseDate = today + 60 days (placeholder)
	∙	APIPartnerID = “AFCO” or “USPF” based on source
	∙	Upsert on QuoteId column (re-submitting the same quote updates rather than duplicates)
	∙	Batch Kickoff: Batch size read from AppSetting table (key QuoteUploadBatchSize), capped at max 50. Returns the async job ID to the UI for polling.
	∙	Supporting Endpoints:
	∙	getTemplateDoc() — returns a download URL for a CSV template file (name from AppSetting key TemplateFileName).
	∙	getJobDetails(jobId) — returns batch job status (processed items, total items, error count) for UI polling.
2.3 Requirements



|ID  |Requirement                           |Details                                                                                                                                                                                                                          |
|----|--------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|R1.1|REST API endpoint for quote submission|Accept a JSON payload of {quoteId: agtCode} pairs + source string. Return a job/correlation ID for status tracking.                                                                                                              |
|R1.2|Account resolution by AGT code        |Query the Account table to match AGT codes to Account IDs: `SELECT Id FROM Account WHERE AGTCode IN (@codes)`. Use a configurable fallback Account when no match is found.                                                       |
|R1.3|Configurable settings                 |Externalize: fallback AGT code, batch size cap (max 50), template file name. Store in AppSetting table.                                                                                                                          |
|R1.4|Upsert placeholder quotes             |Create or update Quote records using SQL MERGE on QuoteId. Populate: Name, StageName, AGT info, AccountId, APIPartnerID, CloseDate (today+60).                                                                                   |
|R1.5|Async job dispatch                    |Enqueue a background job (e.g., Hangfire, Azure Service Bus, or .NET BackgroundService) that processes quotes in configurable batch sizes. Return the job ID to the caller immediately. Track job state in the JobTracking table.|
|R1.6|Template download endpoint            |Serve a CSV template file from a configurable location (blob storage or file system).                                                                                                                                            |
|R1.7|Job status polling endpoint           |Expose GET endpoint that reads from JobTracking and returns: status (Queued/Processing/Completed/Failed), items processed, total items, error count.                                                                             |

3. Step 2: Quote Enrichment via FinancePro SOAP API
3.1 Functional Description
For each placeholder quote, the system calls the FinancePro SOAP web service to retrieve full quote details and a PDF agreement URL, then maps all data onto the Quote record and creates child Policy records. It also prepares a PubSub notification message for each successful quote.
3.2 Authentication & API Setup
Credentials come from the LenderConfiguration table, selected by source name (“AFCO” or “USPF”).



|Field                |Purpose                                               |
|---------------------|------------------------------------------------------|
|Username             |SOAP login name                                       |
|PasswordSecretName   |Reference to secret store for SOAP login password     |
|ImporterKeySecretName|Reference to secret store for API key                 |
|EndpointUrl          |Base URL, appended with /webservices/QuoteService.asmx|

A Login object is built with placeholder values: FirstName (“First”), LastName (“Last”), Phone (“Mobile”), Email (“fake@email.test”), and UserID = 0.
An AuthHeader SOAP header is constructed with Key, Login, SiteID = 0, LogImport = false.
3.3 SOAP API Calls
Two SOAP calls per quote:
	1.	GetQuoteByID(quoteId) — returns a Results object containing the full Quote with nested Insured, Agency, Policies[].
	2.	GetQuoteAgreementURL(quoteId) — returns a URL string for the finance agreement PDF.
SOAP endpoint: {EndpointUrl}/webservices/QuoteService.asmx
WSDL namespace: http://www.financepro.com/financepro/WebServices/
3.4 Error Handling
	∙	API-level errors (Results.Errors.Error[] is non-null): Set IntegrationError = true. Capture first error’s: ErrorCode, Description, Message, Severity, PolicyID. Skip all enrichment — the quote stays in its placeholder state with error flags.
	∙	Unhandled exceptions: Set IntegrationError = true. Capture: exception type name as ErrorCode, stack trace as ErrorDescription, message as ErrorMessage, “Fatal” as ErrorSeverity.
3.5 Quote Field Mapping (Success Path)



|Quote Column               |Source                            |Notes                         |
|---------------------------|----------------------------------|------------------------------|
|Name                       |QuoteNumber + “ “ + Insured.Name  |Overwrites placeholder name   |
|QuoteNumber                |Quote.QuoteNumber                 |                              |
|StageName                  |“Quote Released to RT/Agent”      |Hardcoded                     |
|QuoteStatus                |“New Quote”                       |Hardcoded                     |
|APIPartnerID               |source (“USPF” or “AFCO”)         |                              |
|QuoteApprover              |“Ryan Integration”                |Hardcoded                     |
|FirstNamedInsured          |Quote.Insured.Name                |                              |
|InsuredInReceivership      |“No”                              |Hardcoded default             |
|RetailAgentContactName     |Quote.Agency.ContactName          |                              |
|EffectiveDate              |Quote.EffectiveDate (date only)   |                              |
|CloseDate                  |Quote.EffectiveDate (date only)   |Overwrites placeholder        |
|PhoneNumber                |Quote.Insured.Phone               |                              |
|EmailAddress               |Quote.Insured.Email               |                              |
|MailingStreet              |Insured.Address1                  |                              |
|MailingCity                |Insured.City                      |                              |
|MailingState               |Insured.State                     |                              |
|MailingPostalCode          |Insured.Zip                       |                              |
|MailingCountry             |Insured.Country                   |                              |
|PhysicalStreet             |Insured.Address1                  |Same as mailing               |
|PhysicalCity               |Insured.City                      |Same as mailing               |
|PhysicalState              |Insured.State                     |Same as mailing               |
|PhysicalPostalCode         |Insured.Zip                       |Same as mailing               |
|PhysicalCountry            |Insured.Country                   |Same as mailing               |
|TotalPaymentAmount         |NumberOfPayments * PaymentAmount  |Calculated                    |
|NumberOfInstallments       |Quote.NumberOfPayments            |                              |
|DownPayment                |Quote.DownPayment                 |                              |
|InstallmentAmount          |Quote.PaymentAmount               |                              |
|AmountFinanced             |Quote.AmountFinanced              |                              |
|APR                        |Quote.InterestRate                |                              |
|FinanceCharge              |Quote.FinanceCharge               |                              |
|AgentReferralFeeAmount     |Quote.AgentCompensation           |                              |
|DocStampTax                |Quote.DocStampFee                 |                              |
|QuotingForReferralFeeAmount|Quote.AgentCompensation           |Same as AgentReferralFeeAmount|
|AgentCode                  |Quote.Agency.SearchCode           |                              |
|PolicyCount                |0, or count of policies if present|                              |
|FirstInstallmentDueDate    |Quote.FirstPaymentDue (date only) |                              |
|FinanceAgreementPdfUrl     |URL from GetQuoteAgreementURL     |                              |
|IntegrationError           |false                             |Cleared on success            |
|ErrorCode                  |“”                                |Cleared on success            |
|ErrorDescription           |“”                                |Cleared on success            |
|ErrorMessage               |“”                                |Cleared on success            |
|ErrorSeverity              |“”                                |Cleared on success            |
|ErrorPolicyId              |NULL                              |Cleared on success            |

3.6 Policy Aggregation & Mapping
When Quote.Policies.Policy[] is non-empty:
Aggregate calculations across all policies (written to parent Quote):



|Quote Column         |Calculation                                                                                               |
|---------------------|----------------------------------------------------------------------------------------------------------|
|QuoteLastModifiedDate|MAX of all Policy.LastModifiedDate                                                                        |
|QuoteCreateDate      |MIN of all Policy.DateEntered                                                                             |
|NonRefundableFee     |SUM of (PolicyFee + BrokerFee + InspectionFee) across all policies                                        |
|TotalPremium         |Quote.TotalGrossPremium + SUM of (PolicyFee + BrokerFee + InspectionFee + TaxStampFee) across all policies|
|PolicyCount          |Count of policies                                                                                         |
|RyanCompanyName      |First policy’s EnteredByUserName                                                                          |
|QuoteCreateUser      |First policy’s EnteredByUserName                                                                          |

Per-policy child record (Policy table):



|Policy Column          |Source                                                   |
|-----------------------|---------------------------------------------------------|
|Name                   |Policy.PolicyNumber                                      |
|PolicyNumber           |Policy.PolicyNumber (upsert key)                         |
|QuoteId                |FK to parent Quote record                                |
|CoverageCode           |Policy.CoverageType                                      |
|EffectiveDate          |Policy.InceptionDate (date only, nullable)               |
|PolicyTerm             |Policy.PolicyTerm                                        |
|CarrierCode            |Policy.InsuranceCompany?.SearchCode (nullable navigation)|
|GACode                 |Policy.GeneralAgency?.SearchCode (nullable navigation)   |
|Premium                |Policy.GrossPremium                                      |
|MinimumEarnedPercentage|Policy.MinEarnedPercent                                  |
|IsAuditable            |Policy.Auditable                                         |

3.7 Database Persistence
Quotes: UPDATE with partial success (individual row failures must not abort the batch). Failed saves are tracked in a failures map keyed by Quote ID with Code/Description/Message.
Policies: MERGE on PolicyNumber with partial success. Failures logged.
3.8 Finish / Post-Processing
After all batches complete:
	1.	Any Quotes that failed DB save during processing get updated with error flags (IntegrationError = true, error details, ErrorSeverity = “Fatal”).
	2.	The accumulated PubSub messages (one per successful quote) are passed to the publishing step and dispatched as an async job.
3.9 Requirements



|ID   |Requirement                        |Details                                                                                                                                                                                                                                                        |
|-----|-----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|R2.1 |FinancePro SOAP client             |Generate or hand-code a WCF/HttpClient-based client for the FinancePro QuoteService.asmx. Must support SOAP header authentication (AuthHeader with Key, Login, SiteID). Two operations: GetQuoteByID(int) and GetQuoteAgreementURL(int).                       |
|R2.2 |Multi-tenant credential management |Store USPF and AFCO credentials separately in the LenderConfiguration table (username, endpoint URL) with passwords and importer keys referenced via secret store names (Azure Key Vault, AWS Secrets Manager). Load by source name at runtime.                |
|R2.3 |Per-quote processing with isolation|Process each quote independently. One quote’s failure must not block or roll back others. Wrap each quote in a try/catch.                                                                                                                                      |
|R2.4 |Quote-level field mapping          |Implement the full mapping table from section 3.5. Map SOAP response fields to Quote table columns. Physical and mailing addresses are set identically from the Insured object.                                                                                |
|R2.5 |Policy aggregation logic           |Iterate all policies on a quote to compute: MAX(LastModifiedDate), MIN(DateEntered), SUM(PolicyFee+BrokerFee+InspectionFee), SUM(PolicyFee+BrokerFee+InspectionFee+TaxStampFee) + TotalGrossPremium. Set QuoteCreateUser from first policy’s EnteredByUserName.|
|R2.6 |Policy child record mapping        |Map each policy to a row in the Policy table per section 3.6. Handle nullable navigations (InsuranceCompany?.SearchCode, GeneralAgency?.SearchCode).                                                                                                           |
|R2.7 |Upsert semantics                   |Quotes: UPDATE by internal ID (created in Step 1). Policies: MERGE on PolicyNumber. Both must use partial-success mode — do not abort the batch on a single record failure.                                                                                    |
|R2.8 |API error propagation              |When Results.Errors.Error[] is non-null, capture the first error’s Code/Description/Message/Severity/PolicyID onto the Quote record and mark IntegrationError = true.                                                                                          |
|R2.9 |Exception error propagation        |For unhandled exceptions: capture type name as ErrorCode, stack trace as ErrorDescription, message as ErrorMessage, ErrorSeverity = “Fatal”.                                                                                                                   |
|R2.10|Save-failure writeback             |Track any DB save failures during processing. After all items complete, UPDATE the failed Quote records with error details directly in SQL Server.                                                                                                             |
|R2.11|PubSub message assembly            |For each successfully enriched quote, build a USPFPubSubMessage (see Step 3). Accumulate across batches and pass to the publishing step.                                                                                                                       |
|R2.12|Batch size control                 |Process quotes in configurable batch sizes (max 50). Each batch makes 2 SOAP calls per quote, so a batch of 50 = 100 outbound HTTP calls — plan for timeouts and connection pooling accordingly.                                                               |

4. Step 3: GCP Pub/Sub Notification
4.1 Functional Description
After all quotes are enriched, the system authenticates to Google Cloud Platform using a service account JWT and publishes document metadata messages to a GCP Pub/Sub topic (or a Pub/Sub REST proxy endpoint). These messages notify downstream systems (ImageRight document management) that PFA documents are available.
4.2 GCP Authentication
Credentials from PubSubConfiguration table:



|Field               |Purpose                                           |
|--------------------|--------------------------------------------------|
|ClientEmail         |GCP service account email                         |
|Scope               |OAuth scope                                       |
|TokenEndpoint       |Google OAuth token endpoint (audience & token URL)|
|FunctionEndpoint    |The HTTP endpoint to POST messages to             |
|PrivateKeySecretName|Reference to secret store for the signing key     |

JWT construction: aud = TokenEndpoint, iss = ClientEmail, additional claim: scope = Scope value. Signed using the service account’s private key (retrieved from secret store via PrivateKeySecretName). JWT bearer token exchange returns an access_token.
4.3 Message Structure

{
  "sourceSystem": "Stetson",
  "sourceSystemId": "{QuoteNumber}",
  "sourceSystemEnvironment": "PROD",
  "documentOwner": "Stetson",
  "documentOwnerId": "{QuoteNumber}",
  "documentCategory": "PFA",
  "documentType": "PFA",
  "documentDescription": "{Insured.Name}",
  "description": "PFA",
  "fileName": "{QuoteNumber}_PFA.pdf",
  "createdAt": "yyyy-MM-ddThh:mm:ss.SS",
  "lastModifiedAt": "yyyy-MM-ddThh:mm:ss.SS",
  "receivedAt": "yyyy-MM-ddThh:mm:ss.SS",
  "sentOn": "yyyy-MM-ddThh:mm:ss.SS",
  "isDeleted": false,
  "versions": [{
    "version": 0,
    "url": "{FinanceAgreementPdfUrl}",
    "hash": "w9HgCM5HAWhIQpBUh8Zvbg==",
    "size": 16812,
    "uuid": "5bdd543d-cd14-4af3-a778-02c91b098bda"
  }],
  "imageRightFileAttributes": [
    {"name": "Lender", "type": "string", "value": "{USPF|AFCO}"},
    {"name": "AGT_Code", "type": "string", "value": "{AGTNumber}"},
    {"name": "Effective", "type": "datetime", "value": "{EffectiveDate}"},
    {"name": "Expiration", "type": "datetime", "value": "{EffectiveDate}"},
    {"name": "Amount_Financed", "type": "string", "value": "{AmountFinanced}"},
    {"name": "AGENT", "type": "string", "value": "{Agency.SearchCode}"}
  ]
}


Notes: Expiration is set to the same value as Effective (preserve this behavior). versions[0].hash, .size, and .uuid are hardcoded placeholder values. All four timestamp fields are set to current time at message construction.
4.4 Message Publishing
Messages are chunked into batches of 1000. Each batch is sent as a single HTTP POST to the configured FunctionEndpoint.
Request format (Google Pub/Sub REST API compatible):

{
  "messages": [
    {
      "attributes": { "Content-Type": "application/json" },
      "data": "<base64-encoded JSON of USPFPubSubMessage>"
    }
  ]
}


Each message’s JSON is serialized, converted to bytes, then Base64 encoded into the data field. Auth: Authorization: Bearer {access_token}. Errors are logged but do not throw — fire-and-forget per batch.
4.5 Requirements



|ID  |Requirement                           |Details                                                                                                                                                                                                                                    |
|----|--------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|R3.1|GCP service account JWT authentication|Build a JWT with aud = token endpoint, iss = service account email, custom scope claim. Sign with the service account’s private key. Exchange via HTTP POST for an access_token. Consider using the Google.Apis.Auth NuGet package.        |
|R3.2|PubSub message data model             |Implement the USPFPubSubMessage class exactly as shown in section 4.3. All hardcoded defaults must match. Timestamp format: yyyy-MM-ddThh:mm:ss.SS.                                                                                        |
|R3.3|Message construction                  |For each successful quote, build a message with: sourceSystemId/documentOwnerId = QuoteNumber, documentDescription = Insured Name, fileName = {QuoteNumber}_PFA.pdf, versions[0].url = agreement PDF URL, and 6 ImageRight file attributes.|
|R3.4|Batched HTTP publishing               |Chunk messages into groups of 1000. For each chunk, POST to the configured endpoint with the Pub/Sub REST format. Each message’s data is the base64 encoding of its JSON serialization.                                                    |
|R3.5|Resilient publishing                  |Each batch POST should have error handling — log failures but continue to the next batch. Consider retry with exponential backoff for transient failures (429, 503). Do not fail the entire job if one batch fails.                        |
|R3.6|Configurable Pub/Sub settings         |Store in PubSubConfiguration table: service account email, OAuth scope, token endpoint URL, function/publish endpoint URL, private key secret name. Support swapping between environments (prod/sandbox).                                  |

5. Cross-Cutting Requirements



|ID   |Requirement               |Details                                                                                                                                                                                                           |
|-----|--------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
|R4.1 |SQL Server data store     |All reads and writes target SQL Server directly. No Salesforce API integration. Schema includes: Account, Quote, Policy, JobTracking, LenderConfiguration, PubSubConfiguration, AppSetting. Use EF Core or Dapper.|
|R4.2 |Structured logging        |Replace debug logging with structured logging (Serilog, NLog). Include correlation IDs per job and per quote.                                                                                                     |
|R4.3 |Observability             |Expose metrics: quotes processed, quotes failed, API call latency, PubSub publish success/failure counts.                                                                                                         |
|R4.4 |Idempotency               |Use MERGE on QuoteId for quotes and MERGE on PolicyNumber for policies. Re-running the same job with the same inputs must produce the same result without duplicates.                                             |
|R4.5 |Rate limiting / throttling|FinancePro SOAP API gets called 2x per quote. At 50 quotes/batch, that’s 100 calls. Implement configurable concurrency limits.                                                                                    |
|R4.6 |Secret management         |FinancePro credentials and GCP service account private key must be stored in a secrets manager (Azure Key Vault, AWS Secrets Manager). Referenced by name — never stored in plain text.                           |
|R4.7 |Retry policies            |Add configurable retry policies (Polly) for transient SOAP/HTTP failures on FinancePro calls and GCP Pub/Sub POSTs. DB operations are local and do not require retry.                                             |
|R4.8 |Data validation           |Add input validation on user-submitted quote IDs (numeric, reasonable range) and defensive null checks on SOAP API response fields before mapping.                                                                |
|R4.9 |Database migrations       |Use EF Core Migrations or a tool like DbUp/FluentMigrator to version the schema. Include seed data for AppSetting, LenderConfiguration, PubSubConfiguration, and the fallback Account row.                        |
|R4.10|Transaction boundaries    |Each quote’s enrichment (Quote UPDATE + Policy MERGE) should be in a single DB transaction. Failures on one quote must not roll back other quotes in the same batch.                                              |
|R4.11|Concurrency               |Use optimistic concurrency on Quote.LastModifiedDate (EF Core concurrency token or ROWVERSION column) to prevent lost updates.                                                                                    |

6. Summary of External Dependencies



|System                |Protocol                 |Operations Used                                           |
|----------------------|-------------------------|----------------------------------------------------------|
|FinancePro (USPF/AFCO)|SOAP over HTTPS          |GetQuoteByID(int), GetQuoteAgreementURL(int)              |
|Google Cloud Platform |REST (OAuth2 JWT)        |Token exchange, then POST to Pub/Sub endpoint             |
|SQL Server            |Direct (EF Core / Dapper)|CRUD on Quote, MERGE on Policy, Query Account, JobTracking|
|ImageRight            |Indirect (via Pub/Sub)   |Consumes the published messages downstream                |

7. SQL Server Schema
Account



|Column |Type         |Constraints    |
|-------|-------------|---------------|
|Id     |INT IDENTITY |PK             |
|Name   |NVARCHAR(255)|NOT NULL       |
|AGTCode|NVARCHAR(50) |UNIQUE, INDEXED|

Quote



|Column                     |Type          |Constraints         |
|---------------------------|--------------|--------------------|
|Id                         |INT IDENTITY  |PK                  |
|QuoteId                    |NVARCHAR(50)  |UNIQUE, NOT NULL    |
|QuoteNumber                |NVARCHAR(50)  |                    |
|Name                       |NVARCHAR(255) |NOT NULL            |
|AccountId                  |INT           |FK → Account.Id     |
|AGTNumber                  |NVARCHAR(50)  |                    |
|QuotingForCode             |NVARCHAR(50)  |                    |
|QuotingForAltCode          |NVARCHAR(50)  |                    |
|APIPartnerID               |NVARCHAR(10)  |NOT NULL            |
|StageName                  |NVARCHAR(100) |NOT NULL            |
|QuoteStatus                |NVARCHAR(100) |                    |
|QuoteApprover              |NVARCHAR(100) |                    |
|FirstNamedInsured          |NVARCHAR(255) |                    |
|InsuredInReceivership      |NVARCHAR(10)  |                    |
|RetailAgentContactName     |NVARCHAR(255) |                    |
|EffectiveDate              |DATE          |                    |
|CloseDate                  |DATE          |NOT NULL            |
|PhoneNumber                |NVARCHAR(50)  |                    |
|EmailAddress               |NVARCHAR(255) |                    |
|MailingStreet              |NVARCHAR(500) |                    |
|MailingCity                |NVARCHAR(100) |                    |
|MailingState               |NVARCHAR(10)  |                    |
|MailingPostalCode          |NVARCHAR(20)  |                    |
|MailingCountry             |NVARCHAR(10)  |                    |
|PhysicalStreet             |NVARCHAR(500) |                    |
|PhysicalCity               |NVARCHAR(100) |                    |
|PhysicalState              |NVARCHAR(10)  |                    |
|PhysicalPostalCode         |NVARCHAR(20)  |                    |
|PhysicalCountry            |NVARCHAR(10)  |                    |
|TotalPaymentAmount         |DECIMAL(18,2) |                    |
|NumberOfInstallments       |INT           |                    |
|DownPayment                |DECIMAL(18,2) |                    |
|InstallmentAmount          |DECIMAL(18,2) |                    |
|AmountFinanced             |DECIMAL(18,2) |                    |
|APR                        |DECIMAL(8,4)  |                    |
|FinanceCharge              |DECIMAL(18,2) |                    |
|AgentReferralFeeAmount     |DECIMAL(18,2) |                    |
|DocStampTax                |DECIMAL(18,2) |                    |
|QuotingForReferralFeeAmount|DECIMAL(18,2) |                    |
|AgentCode                  |NVARCHAR(50)  |                    |
|PolicyCount                |INT           |DEFAULT 0           |
|FirstInstallmentDueDate    |DATE          |                    |
|FinanceAgreementPdfUrl     |NVARCHAR(2000)|                    |
|TotalPremium               |DECIMAL(18,2) |                    |
|NonRefundableFee           |DECIMAL(18,2) |                    |
|QuoteLastModifiedDate      |DATE          |                    |
|QuoteCreateDate            |DATE          |                    |
|QuoteCreateUser            |NVARCHAR(255) |                    |
|RyanCompanyName            |NVARCHAR(255) |                    |
|IntegrationError           |BIT           |DEFAULT 0           |
|ErrorCode                  |NVARCHAR(255) |                    |
|ErrorDescription           |NVARCHAR(MAX) |                    |
|ErrorMessage               |NVARCHAR(MAX) |                    |
|ErrorSeverity              |NVARCHAR(50)  |                    |
|ErrorPolicyId              |INT           |                    |
|CreatedDate                |DATETIME2     |DEFAULT GETUTCDATE()|
|LastModifiedDate           |DATETIME2     |DEFAULT GETUTCDATE()|

Indexes: UNIQUE on QuoteId, FK index on AccountId, index on AGTNumber.
Policy



|Column                 |Type         |Constraints            |
|-----------------------|-------------|-----------------------|
|Id                     |INT IDENTITY |PK                     |
|QuoteId                |INT          |FK → Quote.Id, NOT NULL|
|PolicyNumber           |NVARCHAR(100)|UNIQUE, NOT NULL       |
|Name                   |NVARCHAR(255)|                       |
|CoverageCode           |NVARCHAR(50) |                       |
|EffectiveDate          |DATE         |                       |
|PolicyTerm             |DECIMAL(10,2)|                       |
|CarrierCode            |NVARCHAR(50) |                       |
|GACode                 |NVARCHAR(50) |                       |
|Premium                |DECIMAL(18,2)|                       |
|MinimumEarnedPercentage|DECIMAL(8,4) |                       |
|IsAuditable            |BIT          |                       |
|CreatedDate            |DATETIME2    |DEFAULT GETUTCDATE()   |
|LastModifiedDate       |DATETIME2    |DEFAULT GETUTCDATE()   |

Indexes: UNIQUE on PolicyNumber, FK index on QuoteId.
JobTracking



|Column        |Type            |Constraints         |
|--------------|----------------|--------------------|
|Id            |UNIQUEIDENTIFIER|PK, DEFAULT NEWID() |
|Status        |NVARCHAR(50)    |NOT NULL            |
|TotalItems    |INT             |NOT NULL            |
|ItemsProcessed|INT             |DEFAULT 0           |
|NumberOfErrors|INT             |DEFAULT 0           |
|Source        |NVARCHAR(10)    |                    |
|CreatedDate   |DATETIME2       |DEFAULT GETUTCDATE()|
|CompletedDate |DATETIME2       |NULL                |

LenderConfiguration



|Column               |Type         |Constraints|
|---------------------|-------------|-----------|
|Id                   |INT IDENTITY |PK         |
|Name                 |NVARCHAR(50) |UNIQUE     |
|Username             |NVARCHAR(255)|           |
|PasswordSecretName   |NVARCHAR(255)|           |
|ImporterKeySecretName|NVARCHAR(255)|           |
|EndpointUrl          |NVARCHAR(500)|           |

PubSubConfiguration



|Column              |Type         |Constraints|
|--------------------|-------------|-----------|
|Id                  |INT IDENTITY |PK         |
|ClientEmail         |NVARCHAR(255)|           |
|Scope               |NVARCHAR(500)|           |
|TokenEndpoint       |NVARCHAR(500)|           |
|FunctionEndpoint    |NVARCHAR(500)|           |
|PrivateKeySecretName|NVARCHAR(255)|           |

AppSetting



|Column|Type         |Constraints|
|------|-------------|-----------|
|Key   |NVARCHAR(100)|PK         |
|Value |NVARCHAR(500)|NOT NULL   |

Seed data:



|Key                 |Value                       |
|--------------------|----------------------------|
|QuoteUploadBatchSize|50                          |
|PlaceholderAGTCode  |(configured per environment)|
|RecordType          |Stetson                     |
|TemplateFileName    |(configured per environment)|
