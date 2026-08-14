namespace Finance.Domain.ValueObjects;

public enum ExpenseCategory
{
    Rent = 0,
    Utilities = 1,
    Groceries = 2,
    Transportation = 3,
    Entertainment = 4,
    Healthcare = 5,
    Insurance = 6,
    Subscriptions = 7,
    Internet = 8,
    Phone = 9,
    Other = 10
}

/// <summary>
/// Reading a category off the wire. It stayed a free string on the DTOs for API compatibility, so
/// somebody has to turn it into the enum — and it was being done in three places with two answers:
/// the manager silently fell back to Other, the validator rejected. One of those has to be the
/// rule, and it is the validator's: a category nobody recognises is a typo, and quietly filing it
/// under Other loses what the person meant.
/// </summary>
public static class ExpenseCategories
{
    public static bool IsKnown(string? category) =>
        Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out _);

    /// <summary>Throws on anything unrecognised — the validator has already rejected it at the
    /// edge, so reaching here with a bad one is a bug rather than bad input.</summary>
    public static ExpenseCategory Parse(string? category) =>
        Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{category}' is not an expense category.", nameof(category));
}
