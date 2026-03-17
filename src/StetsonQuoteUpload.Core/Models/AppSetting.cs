namespace StetsonQuoteUpload.Core.Models;

public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public static class AppSettingKeys
{
    public const string QuoteUploadBatchSize = "QuoteUploadBatchSize";
    public const string PlaceholderAGTCode = "PlaceholderAGTCode";
    public const string TemplateFileName = "TemplateFileName";
    public const string RecordType = "RecordType";
}
