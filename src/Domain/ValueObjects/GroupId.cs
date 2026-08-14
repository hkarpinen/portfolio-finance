namespace Finance.Domain.ValueObjects;

// Issued elsewhere — finance treats it as an opaque Guid and never resolves it.
public readonly record struct GroupId(Guid Value)
{
    public static GroupId Create(Guid value) => new(value);
}
