namespace Finance.Domain.ValueObjects;

/// <summary>
/// Household aggregate root identifier.
/// </summary>
public readonly record struct HouseholdId(Guid Value)
{
    public static HouseholdId New() => new(Guid.NewGuid());
    public static HouseholdId Create(Guid value) => new(value);
}
