namespace Bills.Domain.ValueObjects;

/// <summary>
/// Income source aggregate root identifier.
/// </summary>
public readonly record struct IncomeId(Guid Value)
{
    public static IncomeId New() => new(Guid.NewGuid());
    public static IncomeId Create(Guid value) => new(value);
}
