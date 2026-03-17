using System.Text.Json.Serialization;

namespace StetsonQuoteUpload.Core.Models;

public class USPFPubSubMessage
{
    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; set; } = "Stetson";

    [JsonPropertyName("sourceSystemId")]
    public string SourceSystemId { get; set; } = string.Empty;

    [JsonPropertyName("sourceSystemEnvironment")]
    public string SourceSystemEnvironment { get; set; } = "PROD";

    [JsonPropertyName("documentOwner")]
    public string DocumentOwner { get; set; } = "Stetson";

    [JsonPropertyName("documentOwnerId")]
    public string DocumentOwnerId { get; set; } = string.Empty;

    [JsonPropertyName("documentCategory")]
    public string DocumentCategory { get; set; } = "PFA";

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PFA";

    [JsonPropertyName("documentDescription")]
    public string DocumentDescription { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = "PFA";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("lastModifiedAt")]
    public string LastModifiedAt { get; set; } = string.Empty;

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = string.Empty;

    [JsonPropertyName("sentOn")]
    public string SentOn { get; set; } = string.Empty;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [JsonPropertyName("versions")]
    public List<PubSubVersion> Versions { get; set; } = new();

    [JsonPropertyName("imageRightFileAttributes")]
    public List<PubSubFileAttribute> ImageRightFileAttributes { get; set; } = new();
}

public class PubSubVersion
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 0;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "w9HgCM5HAWhIQpBUh8Zvbg==";

    [JsonPropertyName("size")]
    public int Size { get; set; } = 16812;

    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "5bdd543d-cd14-4af3-a778-02c91b098bda";
}

public class PubSubFileAttribute
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
