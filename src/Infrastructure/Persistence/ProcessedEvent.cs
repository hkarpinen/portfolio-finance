namespace Infrastructure.Persistence;

public sealed class ProcessedEvent
{
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }

    private ProcessedEvent()
    {
    }

    public ProcessedEvent(Guid eventId, string eventType, DateTime processedAt)
    {
        EventId = eventId;
        EventType = eventType;
        ProcessedAt = processedAt;
    }
}
