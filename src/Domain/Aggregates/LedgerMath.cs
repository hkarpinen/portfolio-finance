using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Pure derivations over postings — balances and the trial balance. Balances are NEVER
/// stored; they are always computed from the journal, so the sub-ledger can never drift
/// from its source entries.
/// </summary>
public static class LedgerMath
{
    /// <summary>
    /// An account's balance from its postings, oriented to its normal balance (P3): a
    /// debit-normal account (Asset/Expense) shows a positive balance when net-debited; a
    /// credit-normal account (Liability/Equity/Income) when net-credited.
    /// </summary>
    public static decimal AccountBalance(NormalBalance normal, IEnumerable<Posting> postings)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var p in postings)
        {
            if (p.Direction == EntryDirection.Debit) debits += p.Amount.Amount;
            else credits += p.Amount.Amount;
        }
        return normal == NormalBalance.Debit ? debits - credits : credits - debits;
    }

    /// <summary>
    /// Trial balance (P5): total debits and total credits across a set of postings. A
    /// healthy ledger always satisfies Debits == Credits; the difference is the imbalance.
    /// </summary>
    public static (decimal Debits, decimal Credits) TrialBalance(IEnumerable<Posting> postings)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var p in postings)
        {
            if (p.Direction == EntryDirection.Debit) debits += p.Amount.Amount;
            else credits += p.Amount.Amount;
        }
        return (debits, credits);
    }

    /// <summary>True when total debits equal total credits — the ledger reconciles.</summary>
    public static bool IsBalanced(IEnumerable<Posting> postings)
    {
        var (d, c) = TrialBalance(postings);
        return d == c;
    }
}
