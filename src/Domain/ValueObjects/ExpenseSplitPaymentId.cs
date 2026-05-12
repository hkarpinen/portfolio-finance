namespace Finance.Domain.ValueObjects;

public readonly record struct ExpenseSplitPaymentId(Guid Value)
{
    public static ExpenseSplitPaymentId New() => new(Guid.NewGuid());
    public static ExpenseSplitPaymentId Create(Guid value) => new(value);
}
