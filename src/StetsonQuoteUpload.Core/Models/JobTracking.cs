namespace StetsonQuoteUpload.Core.Models;

public class JobTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Status { get; set; } = JobStatus.Queued;
    public int TotalItems { get; set; }
    public int ItemsProcessed { get; set; }
    public int NumberOfErrors { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }
}

public static class JobStatus
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
