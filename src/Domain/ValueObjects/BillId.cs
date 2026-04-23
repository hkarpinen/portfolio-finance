namespace Bills.Domain.ValueObjects;

/// <summary>
/// Bill aggregate root identifier.
/// </summary>
public readonly record struct BillId(Guid Value)
{
    public static BillId New() => new(Guid.NewGuid());
    public static BillId Create(Guid value) => new(value);
}
