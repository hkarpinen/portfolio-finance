namespace Finance.Domain.ValueObjects;

// Owned by the household service — finance treats it as an opaque Guid and never resolves it.
public readonly record struct GroupId(Guid Value)
{
    public static GroupId Create(Guid value) => new(value);
}
