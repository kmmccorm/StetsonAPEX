namespace StetsonQuoteUpload.Core.Models;

public class PubSubConfiguration
{
    public int Id { get; set; }
    public string? ClientEmail { get; set; }
    public string? Scope { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? FunctionEndpoint { get; set; }
    public string? PrivateKeySecretName { get; set; }
}
