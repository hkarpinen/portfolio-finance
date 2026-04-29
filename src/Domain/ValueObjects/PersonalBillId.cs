namespace Finance.Domain.ValueObjects;

/// <summary>
/// Personal bill aggregate root identifier.
/// </summary>
public readonly record struct PersonalBillId(Guid Value)
{
    public static PersonalBillId New() => new(Guid.NewGuid());
    public static PersonalBillId Create(Guid value) => new(value);
}
