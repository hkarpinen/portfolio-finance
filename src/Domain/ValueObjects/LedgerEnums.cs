namespace Finance.Domain.ValueObjects;

// Owner ids are opaque — finance knows nothing of "household".
public enum LedgerOwnerType
{
    Group,
    User
}

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense
}

public enum NormalBalance
{
    Debit,
    Credit
}

public enum EntryDirection
{
    Debit,
    Credit
}

public static class AccountTypeExtensions
{
    public static NormalBalance NormalBalance(this AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => Finance.Domain.ValueObjects.NormalBalance.Debit,
        AccountType.Liability or AccountType.Equity or AccountType.Income => Finance.Domain.ValueObjects.NormalBalance.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type.")
    };
}
