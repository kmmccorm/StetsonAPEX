namespace StetsonQuoteUpload.Core.Interfaces;

public interface IAppSettingRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    Task<int> GetIntValueAsync(string key, int defaultValue = 0, CancellationToken ct = default);
}
