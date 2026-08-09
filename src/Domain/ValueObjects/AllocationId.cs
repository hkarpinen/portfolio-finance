namespace Finance.Domain.ValueObjects;

public readonly record struct AllocationId(Guid Value)
{
    public static AllocationId New() => new(Guid.NewGuid());
    public static AllocationId Create(Guid value) => new(value);
}
