-- ============================================================
-- StetsonQuoteUpload — Initial Schema Migration
-- Run via DbUp, FluentMigrator, or manually before first deploy
-- ============================================================

-- Account
CREATE TABLE [dbo].[Account] (
    [Id]      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name]    NVARCHAR(255)     NOT NULL,
    [AGTCode] NVARCHAR(50)      NULL
);
CREATE UNIQUE INDEX [UX_Account_AGTCode] ON [dbo].[Account] ([AGTCode]) WHERE [AGTCode] IS NOT NULL;

-- Quote
CREATE TABLE [dbo].[Quote] (
    [Id]                          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [QuoteId]                     NVARCHAR(50)      NOT NULL,
    [QuoteNumber]                 NVARCHAR(50)      NULL,
    [Name]                        NVARCHAR(255)     NOT NULL,
    [AccountId]                   INT               NOT NULL REFERENCES [dbo].[Account]([Id]),
    [AGTNumber]                   NVARCHAR(50)      NULL,
    [QuotingForCode]              NVARCHAR(50)      NULL,
    [QuotingForAltCode]           NVARCHAR(50)      NULL,
    [APIPartnerID]                NVARCHAR(10)      NOT NULL,
    [StageName]                   NVARCHAR(100)     NOT NULL,
    [QuoteStatus]                 NVARCHAR(100)     NULL,
    [QuoteApprover]               NVARCHAR(100)     NULL,
    [FirstNamedInsured]           NVARCHAR(255)     NULL,
    [InsuredInReceivership]       NVARCHAR(10)      NULL,
    [RetailAgentContactName]      NVARCHAR(255)     NULL,
    [EffectiveDate]               DATE              NULL,
    [CloseDate]                   DATE              NOT NULL,
    [PhoneNumber]                 NVARCHAR(50)      NULL,
    [EmailAddress]                NVARCHAR(255)     NULL,
    [MailingStreet]               NVARCHAR(500)     NULL,
    [MailingCity]                 NVARCHAR(100)     NULL,
    [MailingState]                NVARCHAR(10)      NULL,
    [MailingPostalCode]           NVARCHAR(20)      NULL,
    [MailingCountry]              NVARCHAR(10)      NULL,
    [PhysicalStreet]              NVARCHAR(500)     NULL,
    [PhysicalCity]                NVARCHAR(100)     NULL,
    [PhysicalState]               NVARCHAR(10)      NULL,
    [PhysicalPostalCode]          NVARCHAR(20)      NULL,
    [PhysicalCountry]             NVARCHAR(10)      NULL,
    [TotalPaymentAmount]          DECIMAL(18,2)     NULL,
    [NumberOfInstallments]        INT               NULL,
    [DownPayment]                 DECIMAL(18,2)     NULL,
    [InstallmentAmount]           DECIMAL(18,2)     NULL,
    [AmountFinanced]              DECIMAL(18,2)     NULL,
    [APR]                         DECIMAL(8,4)      NULL,
    [FinanceCharge]               DECIMAL(18,2)     NULL,
    [AgentReferralFeeAmount]      DECIMAL(18,2)     NULL,
    [DocStampTax]                 DECIMAL(18,2)     NULL,
    [QuotingForReferralFeeAmount] DECIMAL(18,2)     NULL,
    [AgentCode]                   NVARCHAR(50)      NULL,
    [PolicyCount]                 INT               NOT NULL DEFAULT 0,
    [FirstInstallmentDueDate]     DATE              NULL,
    [FinanceAgreementPdfUrl]      NVARCHAR(2000)    NULL,
    [TotalPremium]                DECIMAL(18,2)     NULL,
    [NonRefundableFee]            DECIMAL(18,2)     NULL,
    [QuoteLastModifiedDate]       DATE              NULL,
    [QuoteCreateDate]             DATE              NULL,
    [QuoteCreateUser]             NVARCHAR(255)     NULL,
    [RyanCompanyName]             NVARCHAR(255)     NULL,
    [IntegrationError]            BIT               NOT NULL DEFAULT 0,
    [ErrorCode]                   NVARCHAR(255)     NULL,
    [ErrorDescription]            NVARCHAR(MAX)     NULL,
    [ErrorMessage]                NVARCHAR(MAX)     NULL,
    [ErrorSeverity]               NVARCHAR(50)      NULL,
    [ErrorPolicyId]               INT               NULL,
    [JobId]                       UNIQUEIDENTIFIER  NULL,
    [CreatedDate]                 DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    [LastModifiedDate]            DATETIME2         NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX [UX_Quote_QuoteId]     ON [dbo].[Quote] ([QuoteId]);
CREATE        INDEX [IX_Quote_AccountId]   ON [dbo].[Quote] ([AccountId]);
CREATE        INDEX [IX_Quote_AGTNumber]   ON [dbo].[Quote] ([AGTNumber]);
CREATE        INDEX [IX_Quote_JobId]       ON [dbo].[Quote] ([JobId]);

-- Policy
CREATE TABLE [dbo].[Policy] (
    [Id]                     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [QuoteId]                INT               NOT NULL REFERENCES [dbo].[Quote]([Id]) ON DELETE CASCADE,
    [PolicyNumber]           NVARCHAR(100)     NOT NULL,
    [Name]                   NVARCHAR(255)     NULL,
    [CoverageCode]           NVARCHAR(50)      NULL,
    [EffectiveDate]          DATE              NULL,
    [PolicyTerm]             DECIMAL(10,2)     NULL,
    [CarrierCode]            NVARCHAR(50)      NULL,
    [GACode]                 NVARCHAR(50)      NULL,
    [Premium]                DECIMAL(18,2)     NULL,
    [MinimumEarnedPercentage]DECIMAL(8,4)      NULL,
    [IsAuditable]            BIT               NULL,
    [CreatedDate]            DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    [LastModifiedDate]       DATETIME2         NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX [UX_Policy_PolicyNumber] ON [dbo].[Policy] ([PolicyNumber]);
CREATE        INDEX [IX_Policy_QuoteId]      ON [dbo].[Policy] ([QuoteId]);

-- JobTracking
CREATE TABLE [dbo].[JobTracking] (
    [Id]             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [Status]         NVARCHAR(50)     NOT NULL,
    [TotalItems]     INT              NOT NULL,
    [ItemsProcessed] INT              NOT NULL DEFAULT 0,
    [NumberOfErrors] INT              NOT NULL DEFAULT 0,
    [Source]         NVARCHAR(10)     NULL,
    [CreatedDate]    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    [CompletedDate]  DATETIME2        NULL
);

-- LenderConfiguration
CREATE TABLE [dbo].[LenderConfiguration] (
    [Id]                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name]                  NVARCHAR(50)      NOT NULL,
    [Username]              NVARCHAR(255)     NULL,
    [PasswordSecretName]    NVARCHAR(255)     NULL,
    [ImporterKeySecretName] NVARCHAR(255)     NULL,
    [EndpointUrl]           NVARCHAR(500)     NULL
);
CREATE UNIQUE INDEX [UX_LenderConfiguration_Name] ON [dbo].[LenderConfiguration] ([Name]);

-- PubSubConfiguration
CREATE TABLE [dbo].[PubSubConfiguration] (
    [Id]                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [ClientEmail]         NVARCHAR(255)     NULL,
    [Scope]               NVARCHAR(500)     NULL,
    [TokenEndpoint]       NVARCHAR(500)     NULL,
    [FunctionEndpoint]    NVARCHAR(500)     NULL,
    [PrivateKeySecretName]NVARCHAR(255)     NULL
);

-- AppSetting
CREATE TABLE [dbo].[AppSetting] (
    [Key]   NVARCHAR(100) NOT NULL PRIMARY KEY,
    [Value] NVARCHAR(500) NOT NULL
);

-- ============================================================
-- Seed Data
-- ============================================================

-- Default AppSettings
INSERT INTO [dbo].[AppSetting] ([Key], [Value]) VALUES
    ('QuoteUploadBatchSize', '50'),
    ('PlaceholderAGTCode',   'PLACEHOLDER'),  -- Replace with actual code per environment
    ('RecordType',           'Stetson'),
    ('TemplateFileName',     'QuoteUploadTemplate.csv');  -- Replace with actual filename per environment

-- Placeholder Account (required before any quotes can be created)
INSERT INTO [dbo].[Account] ([Name], [AGTCode])
VALUES ('Placeholder Account', 'PLACEHOLDER');  -- AGTCode must match PlaceholderAGTCode above

-- ============================================================
-- Sample LenderConfiguration rows (populate secrets separately)
-- ============================================================
INSERT INTO [dbo].[LenderConfiguration] ([Name], [Username], [PasswordSecretName], [ImporterKeySecretName], [EndpointUrl])
VALUES
    ('USPF', 'uspf_user', 'USPF_PASSWORD', 'USPF_IMPORTER_KEY', 'https://uspf.financepro.com'),
    ('AFCO', 'afco_user', 'AFCO_PASSWORD', 'AFCO_IMPORTER_KEY', 'https://afco.financepro.com');

-- ============================================================
-- Sample PubSubConfiguration row (populate secret separately)
-- ============================================================
INSERT INTO [dbo].[PubSubConfiguration]
    ([ClientEmail], [Scope], [TokenEndpoint], [FunctionEndpoint], [PrivateKeySecretName])
VALUES
    (
        'your-service-account@project.iam.gserviceaccount.com',
        'https://www.googleapis.com/auth/pubsub',
        'https://oauth2.googleapis.com/token',
        'https://us-central1-project.cloudfunctions.net/pubsub-proxy',
        'GCP_PUBSUB_PRIVATE_KEY'
    );
