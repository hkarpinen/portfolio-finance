using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

// The proofs a whole book owes: what it debited, what it credited, and whether they agree.
// Balances are NEVER stored — always computed from the journal, so no derived total can drift
// from the entries it came from.
public static class LedgerMath
{
    /// <summary>
    /// One account's balance, when you hold its orientation but not the account — the read side
    /// projecting many accounts at once. Prefer <see cref="Account.BalanceFrom"/> where you have
    /// the account, which cannot be given the wrong orientation.
    /// </summary>
    public static decimal AccountBalance(NormalBalance normal, IEnumerable<JournalLine> lines)
    {
        var (debits, credits) = TrialBalance(lines);
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
