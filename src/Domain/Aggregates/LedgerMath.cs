using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// Balances are NEVER stored — always computed from the journal, so no derived total can drift
// from the entries it came from.
public static class LedgerMath
{
    // Oriented to the normal balance: a debit-normal account (Asset/Expense) shows positive when
    // net-debited, a credit-normal one (Liability/Equity/Income) when net-credited.
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

    public static bool IsBalanced(IEnumerable<Posting> postings)
    {
        var (d, c) = TrialBalance(postings);
        return d == c;
    }
}
