namespace Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public const int MaxRetryCount = 5;

    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Published { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public bool DeadLettered { get; set; }
}
