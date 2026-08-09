namespace Finance.Domain.ValueObjects;

public readonly record struct ChargeId(Guid Value)
{
    public static ChargeId New() => new(Guid.NewGuid());
    public static ChargeId Create(Guid value) => new(value);
}
