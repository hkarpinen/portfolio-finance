namespace Finance.Domain.ValueObjects;

public readonly record struct ExpensePaymentId(Guid Value)
{
    public static ExpensePaymentId New() => new(Guid.NewGuid());
    public static ExpensePaymentId Create(Guid value) => new(value);
}
