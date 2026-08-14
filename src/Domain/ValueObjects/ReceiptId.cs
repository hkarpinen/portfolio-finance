namespace Finance.Domain.ValueObjects;

public readonly record struct ReceiptId(Guid Value)
{
    public static ReceiptId New() => new(Guid.NewGuid());
    public static ReceiptId Create(Guid value) => new(value);
}
