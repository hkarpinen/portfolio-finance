namespace Finance.Domain.ValueObjects;

public readonly record struct MemberTransferId(Guid Value)
{
    public static MemberTransferId New() => new(Guid.NewGuid());
    public static MemberTransferId Create(Guid value) => new(value);
}
