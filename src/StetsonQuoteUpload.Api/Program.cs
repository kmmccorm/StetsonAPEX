using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using StetsonQuoteUpload.Api.Jobs;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Services;
using StetsonQuoteUpload.Infrastructure.Data;
using StetsonQuoteUpload.Infrastructure.FinancePro;
using StetsonQuoteUpload.Infrastructure.GCP;
using StetsonQuoteUpload.Infrastructure.Repositories;
using StetsonQuoteUpload.Infrastructure.Secrets;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/stetson-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    // EF Core
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Hangfire
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 5;
    });

    // Repositories
    builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
    builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
    builder.Services.AddScoped<IJobTrackingRepository, JobTrackingRepository>();
    builder.Services.AddScoped<IAccountRepository, AccountRepository>();
    builder.Services.AddScoped<ILenderConfigRepository, LenderConfigRepository>();
    builder.Services.AddScoped<IPubSubConfigRepository, PubSubConfigRepository>();
    builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();

    // Infrastructure services
    builder.Services.AddSingleton<ISecretStore, EnvironmentSecretStore>(); // Swap for AzureKeyVaultSecretStore in prod
    builder.Services.AddScoped<IFinanceProClientFactory, FinanceProClientFactory>();
    builder.Services.AddScoped<GcpAuthService>();
    builder.Services.AddScoped<IPubSubPublisher, GcpPubSubPublisher>();

    // Core services
    builder.Services.AddScoped<QuoteIntakeService>();
    builder.Services.AddScoped<QuoteEnrichmentService>();

    // Hangfire jobs
    builder.Services.AddScoped<QuoteEnrichmentJob>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Stetson Quote Upload API", Version = "v1" });
    });

    var app = builder.Build();

    // Migrate DB on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHangfireDashboard("/hangfire");
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
