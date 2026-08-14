using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

/// <summary>Money arrived. Carries everything the books need so the consumer never reloads it.</summary>
public record ReceiptRecorded(
    ReceiptId ReceiptId,
    UserId OwnerUserId,
    Guid IntoAccountId,
    string Source,
    Money Amount,
    DateTime ReceivedOn) : DomainEvent;

public record ReceiptVoided(ReceiptId ReceiptId, Guid OwnerUserId) : DomainEvent;
