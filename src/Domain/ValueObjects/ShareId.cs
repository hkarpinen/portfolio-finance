namespace Finance.Domain.ValueObjects;

public readonly record struct ShareId(Guid Value)
{
    public static ShareId New() => new(Guid.NewGuid());
    public static ShareId Create(Guid value) => new(value);
}
