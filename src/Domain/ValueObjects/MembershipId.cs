namespace Finance.Domain.ValueObjects;

/// <summary>
/// Household membership aggregate root identifier.
/// </summary>
public readonly record struct MembershipId(Guid Value)
{
    public static MembershipId New() => new(Guid.NewGuid());
    public static MembershipId Create(Guid value) => new(value);
}
