namespace StetsonQuoteUpload.Core.Models;

public class LenderConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? PasswordSecretName { get; set; }
    public string? ImporterKeySecretName { get; set; }
    public string? EndpointUrl { get; set; }
}
