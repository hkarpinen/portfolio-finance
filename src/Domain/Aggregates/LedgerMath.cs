using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// Balances are NEVER stored — always computed from the journal, so no derived total can drift
// from the entries it came from.
public static class LedgerMath
{
    // Oriented to the normal balance: a debit-normal account (Asset/Expense) shows positive when
    // net-debited, a credit-normal one (Liability/Equity/Income) when net-credited.
    public static decimal AccountBalance(NormalBalance normal, IEnumerable<JournalLine> lines)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var p in lines)
        {
            if (p.Direction == EntryDirection.Debit) debits += p.Amount.Amount;
            else credits += p.Amount.Amount;
        }
        return normal == NormalBalance.Debit ? debits - credits : credits - debits;
    }

    public static (decimal Debits, decimal Credits) TrialBalance(IEnumerable<JournalLine> lines)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var p in lines)
        {
            if (p.Direction == EntryDirection.Debit) debits += p.Amount.Amount;
            else credits += p.Amount.Amount;
        }
        return (debits, credits);
    }

    public static bool IsBalanced(IEnumerable<JournalLine> lines)
    {
        var (d, c) = TrialBalance(lines);
        return d == c;
    }
}
