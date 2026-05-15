namespace Finance.Domain.ValueObjects;

/// <summary>
/// Opaque identifier for a group (household) from the household microservice.
/// Finance service treats this as an opaque Guid and does not own the group entity.
/// </summary>
public readonly record struct GroupId(Guid Value)
{
    public static GroupId Create(Guid value) => new(value);
}
