using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IPubSubPublisher
{
    Task PublishAsync(IEnumerable<USPFPubSubMessage> messages, CancellationToken ct = default);
}
