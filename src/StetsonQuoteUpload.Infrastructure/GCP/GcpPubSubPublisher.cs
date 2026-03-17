using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Infrastructure.GCP;

public class GcpPubSubPublisher : IPubSubPublisher
{
    private readonly IPubSubConfigRepository _configRepo;
    private readonly ISecretStore _secrets;
    private readonly GcpAuthService _authService;
    private readonly ILogger<GcpPubSubPublisher> _logger;

    private const int BatchSize = 1000;

    public GcpPubSubPublisher(
        IPubSubConfigRepository configRepo,
        ISecretStore secrets,
        GcpAuthService authService,
        ILogger<GcpPubSubPublisher> logger)
    {
        _configRepo = configRepo;
        _secrets = secrets;
        _authService = authService;
        _logger = logger;
    }

    public async Task PublishAsync(IEnumerable<USPFPubSubMessage> messages, CancellationToken ct = default)
    {
        var config = await _configRepo.GetFirstAsync(ct)
            ?? throw new InvalidOperationException("No PubSubConfiguration found");

        var privateKey = await _secrets.GetSecretAsync(config.PrivateKeySecretName!, ct);

        string accessToken;
        try
        {
            accessToken = await _authService.GetAccessTokenAsync(
                config.ClientEmail!,
                config.Scope!,
                config.TokenEndpoint!,
                privateKey,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain GCP access token; aborting PubSub publish");
            return;
        }

        var messageList = messages.ToList();
        _logger.LogInformation("Publishing {Count} messages to PubSub in batches of {BatchSize}", messageList.Count, BatchSize);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        for (int i = 0; i < messageList.Count; i += BatchSize)
        {
            var batch = messageList.Skip(i).Take(BatchSize).ToList();
            var batchNumber = (i / BatchSize) + 1;

            try
            {
                await PublishBatchAsync(http, config.FunctionEndpoint!, batch, batchNumber, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch {BatchNumber} failed; continuing to next batch", batchNumber);
            }
        }
    }

    private async Task PublishBatchAsync(
        HttpClient http,
        string endpoint,
        List<USPFPubSubMessage> batch,
        int batchNumber,
        CancellationToken ct)
    {
        var pubSubMessages = batch.Select(msg =>
        {
            var json = JsonSerializer.Serialize(msg);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            return new
            {
                attributes = new { ContentType = "application/json" },
                data = base64
            };
        });

        var payload = new { messages = pubSubMessages };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Sending batch {BatchNumber} ({Count} messages) to {Endpoint}", batchNumber, batch.Count, endpoint);

        int retryCount = 0;
        const int maxRetries = 3;

        while (true)
        {
            try
            {
                var response = await http.PostAsync(endpoint, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Batch {BatchNumber} published successfully", batchNumber);
                    return;
                }

                var statusCode = (int)response.StatusCode;
                if ((statusCode == 429 || statusCode == 503) && retryCount < maxRetries)
                {
                    retryCount++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                    _logger.LogWarning("Batch {BatchNumber} got {Status}; retrying in {Delay}s (attempt {Attempt}/{Max})",
                        batchNumber, statusCode, delay.TotalSeconds, retryCount, maxRetries);
                    await Task.Delay(delay, ct);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Batch {BatchNumber} failed with {Status}: {Body}", batchNumber, statusCode, body);
                return;
            }
            catch (Exception ex) when (retryCount < maxRetries && ex is not OperationCanceledException)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                _logger.LogWarning(ex, "Batch {BatchNumber} threw exception; retrying in {Delay}s", batchNumber, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
