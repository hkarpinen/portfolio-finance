namespace Finance.Domain.ValueObjects;

public readonly record struct ChargePaymentId(Guid Value)
{
    public static ChargePaymentId New() => new(Guid.NewGuid());
    public static ChargePaymentId Create(Guid value) => new(value);
}
