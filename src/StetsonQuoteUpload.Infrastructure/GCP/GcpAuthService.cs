using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace StetsonQuoteUpload.Infrastructure.GCP;

public class GcpAuthService
{
    private readonly ILogger<GcpAuthService> _logger;

    public GcpAuthService(ILogger<GcpAuthService> logger) => _logger = logger;

    /// <summary>
    /// Builds a signed JWT and exchanges it for a GCP access token.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(
        string clientEmail,
        string scope,
        string tokenEndpoint,
        string privateKeyPem,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var jwt = BuildJwt(clientEmail, scope, tokenEndpoint, privateKeyPem, now);

        using var http = new HttpClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
            new KeyValuePair<string, string>("assertion", jwt)
        });

        var response = await http.PostAsync(tokenEndpoint, content, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GCP token exchange failed: {Status} {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"GCP token exchange failed: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<GcpTokenResponse>(responseBody);
        return tokenResponse?.AccessToken
            ?? throw new InvalidOperationException("GCP token response missing access_token");
    }

    private static string BuildJwt(
        string clientEmail,
        string scope,
        string audience,
        string privateKeyPem,
        DateTimeOffset now)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var rsaKey = new RsaSecurityKey(rsa);
        var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);

        var claims = new Dictionary<string, object>
        {
            ["scope"] = scope
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = clientEmail,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            Expires = now.AddMinutes(60).UtcDateTime,
            SigningCredentials = signingCredentials,
            Claims = claims
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}

internal class GcpTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }
}
