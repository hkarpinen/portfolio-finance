namespace Finance.Domain.ValueObjects;

/// <summary>Who a ledger's book belongs to. Opaque ids — finance knows nothing of "household".</summary>
public enum LedgerOwnerType
{
    Group,
    User
}

/// <summary>The five classical account types. Determines the normal balance (see extension).</summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense
}

/// <summary>The side an account's balance normally sits on.</summary>
public enum NormalBalance
{
    Debit,
    Credit
}

/// <summary>The side a single posting hits.</summary>
public enum EntryDirection
{
    Debit,
    Credit
}

public static class AccountTypeExtensions
{
    /// <summary>
    /// Normal-balance convention (the bedrock of debit/credit): Asset and Expense are
    /// debit-normal (increase with a debit); Liability, Equity and Income are credit-normal.
    /// </summary>
    public static NormalBalance NormalBalance(this AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => Finance.Domain.ValueObjects.NormalBalance.Debit,
        AccountType.Liability or AccountType.Equity or AccountType.Income => Finance.Domain.ValueObjects.NormalBalance.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type.")
    };
}
