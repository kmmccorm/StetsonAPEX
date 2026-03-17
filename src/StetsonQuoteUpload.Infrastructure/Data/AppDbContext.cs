using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<JobTracking> JobTrackings => Set<JobTracking>();
    public DbSet<LenderConfiguration> LenderConfigurations => Set<LenderConfiguration>();
    public DbSet<PubSubConfiguration> PubSubConfigurations => Set<PubSubConfiguration>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Account
        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("Account");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.AGTCode).HasMaxLength(50);
            e.HasIndex(x => x.AGTCode).IsUnique();
        });

        // Quote
        modelBuilder.Entity<Quote>(e =>
        {
            e.ToTable("Quote");
            e.HasKey(x => x.Id);
            e.Property(x => x.QuoteId).HasMaxLength(50).IsRequired();
            e.Property(x => x.QuoteNumber).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(255).IsRequired();
            e.Property(x => x.APIPartnerID).HasMaxLength(10).IsRequired();
            e.Property(x => x.StageName).HasMaxLength(100).IsRequired();
            e.Property(x => x.QuoteStatus).HasMaxLength(100);
            e.Property(x => x.QuoteApprover).HasMaxLength(100);
            e.Property(x => x.FirstNamedInsured).HasMaxLength(255);
            e.Property(x => x.InsuredInReceivership).HasMaxLength(10);
            e.Property(x => x.RetailAgentContactName).HasMaxLength(255);
            e.Property(x => x.PhoneNumber).HasMaxLength(50);
            e.Property(x => x.EmailAddress).HasMaxLength(255);
            e.Property(x => x.MailingStreet).HasMaxLength(500);
            e.Property(x => x.MailingCity).HasMaxLength(100);
            e.Property(x => x.MailingState).HasMaxLength(10);
            e.Property(x => x.MailingPostalCode).HasMaxLength(20);
            e.Property(x => x.MailingCountry).HasMaxLength(10);
            e.Property(x => x.PhysicalStreet).HasMaxLength(500);
            e.Property(x => x.PhysicalCity).HasMaxLength(100);
            e.Property(x => x.PhysicalState).HasMaxLength(10);
            e.Property(x => x.PhysicalPostalCode).HasMaxLength(20);
            e.Property(x => x.PhysicalCountry).HasMaxLength(10);
            e.Property(x => x.TotalPaymentAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DownPayment).HasColumnType("decimal(18,2)");
            e.Property(x => x.InstallmentAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AmountFinanced).HasColumnType("decimal(18,2)");
            e.Property(x => x.APR).HasColumnType("decimal(8,4)");
            e.Property(x => x.FinanceCharge).HasColumnType("decimal(18,2)");
            e.Property(x => x.AgentReferralFeeAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DocStampTax).HasColumnType("decimal(18,2)");
            e.Property(x => x.QuotingForReferralFeeAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AgentCode).HasMaxLength(50);
            e.Property(x => x.AGTNumber).HasMaxLength(50);
            e.Property(x => x.QuotingForCode).HasMaxLength(50);
            e.Property(x => x.QuotingForAltCode).HasMaxLength(50);
            e.Property(x => x.FinanceAgreementPdfUrl).HasMaxLength(2000);
            e.Property(x => x.TotalPremium).HasColumnType("decimal(18,2)");
            e.Property(x => x.NonRefundableFee).HasColumnType("decimal(18,2)");
            e.Property(x => x.QuoteCreateUser).HasMaxLength(255);
            e.Property(x => x.RyanCompanyName).HasMaxLength(255);
            e.Property(x => x.ErrorCode).HasMaxLength(255);
            e.Property(x => x.ErrorDescription);
            e.Property(x => x.ErrorMessage);
            e.Property(x => x.ErrorSeverity).HasMaxLength(50);
            e.Property(x => x.PolicyCount).HasDefaultValue(0);
            e.Property(x => x.IntegrationError).HasDefaultValue(false);
            e.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.LastModifiedDate).HasDefaultValueSql("GETUTCDATE()").IsConcurrencyToken();

            e.HasIndex(x => x.QuoteId).IsUnique();
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.AGTNumber);
            e.HasIndex(x => x.JobId);

            e.HasOne(x => x.Account)
             .WithMany()
             .HasForeignKey(x => x.AccountId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Policy
        modelBuilder.Entity<Policy>(e =>
        {
            e.ToTable("Policy");
            e.HasKey(x => x.Id);
            e.Property(x => x.PolicyNumber).HasMaxLength(100).IsRequired();
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.CoverageCode).HasMaxLength(50);
            e.Property(x => x.PolicyTerm).HasColumnType("decimal(10,2)");
            e.Property(x => x.CarrierCode).HasMaxLength(50);
            e.Property(x => x.GACode).HasMaxLength(50);
            e.Property(x => x.Premium).HasColumnType("decimal(18,2)");
            e.Property(x => x.MinimumEarnedPercentage).HasColumnType("decimal(8,4)");
            e.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.LastModifiedDate).HasDefaultValueSql("GETUTCDATE()");

            e.HasIndex(x => x.PolicyNumber).IsUnique();
            e.HasIndex(x => x.QuoteId);

            e.HasOne(x => x.Quote)
             .WithMany(x => x.Policies)
             .HasForeignKey(x => x.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // JobTracking
        modelBuilder.Entity<JobTracking>(e =>
        {
            e.ToTable("JobTracking");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("NEWID()");
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
            e.Property(x => x.Source).HasMaxLength(10);
            e.Property(x => x.ItemsProcessed).HasDefaultValue(0);
            e.Property(x => x.NumberOfErrors).HasDefaultValue(0);
            e.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
        });

        // LenderConfiguration
        modelBuilder.Entity<LenderConfiguration>(e =>
        {
            e.ToTable("LenderConfiguration");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(50);
            e.Property(x => x.Username).HasMaxLength(255);
            e.Property(x => x.PasswordSecretName).HasMaxLength(255);
            e.Property(x => x.ImporterKeySecretName).HasMaxLength(255);
            e.Property(x => x.EndpointUrl).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // PubSubConfiguration
        modelBuilder.Entity<PubSubConfiguration>(e =>
        {
            e.ToTable("PubSubConfiguration");
            e.HasKey(x => x.Id);
            e.Property(x => x.ClientEmail).HasMaxLength(255);
            e.Property(x => x.Scope).HasMaxLength(500);
            e.Property(x => x.TokenEndpoint).HasMaxLength(500);
            e.Property(x => x.FunctionEndpoint).HasMaxLength(500);
            e.Property(x => x.PrivateKeySecretName).HasMaxLength(255);
        });

        // AppSetting
        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("AppSetting");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(500).IsRequired();
        });
    }
}
