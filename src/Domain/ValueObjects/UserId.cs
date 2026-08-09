namespace Finance.Domain.ValueObjects;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId Create(Guid value) => new(value);
}
