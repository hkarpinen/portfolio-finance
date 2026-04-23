namespace Bills.Domain.ValueObjects;

/// <summary>
/// Bill split aggregate root identifier.
/// </summary>
public readonly record struct SplitId(Guid Value)
{
    public static SplitId New() => new(Guid.NewGuid());
    public static SplitId Create(Guid value) => new(value);
}
