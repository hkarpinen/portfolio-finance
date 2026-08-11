namespace Finance.Domain.ValueObjects;

public readonly record struct ChargeScheduleId(Guid Value)
{
    public static ChargeScheduleId New() => new(Guid.NewGuid());
    public static ChargeScheduleId Create(Guid value) => new(value);
}
