namespace Finance.Domain.ValueObjects;

/// <summary>
/// Charge aggregate root identifier.
/// </summary>
public readonly record struct ChargeId(Guid Value)
{
    public static ChargeId New() => new(Guid.NewGuid());
    public static ChargeId Create(Guid value) => new(value);
}
