using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Queries;

/// <summary>
/// Folding journal lines into the figures a report needs. One sum — by direction — read two ways.
///
/// It was LedgerMath in Domain, which read as though the ledger owed some arithmetic nobody else
/// could do. Only the read side ever calls it: an account's own balance is
/// <see cref="Account.BalanceFrom"/>, and whether an entry balances is settled by
/// <see cref="JournalEntry.Post"/> refusing to build one that does not.
/// </summary>
internal static class LedgerBalanceReads
{
    public static (decimal Debits, decimal Credits) TrialBalance(IEnumerable<JournalLine> lines)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var l in lines)
        {
            if (l.Direction == EntryDirection.Debit) debits += l.Amount.Amount;
            else credits += l.Amount.Amount;
        }
        return (debits, credits);
    }

    /// <summary>
    /// One account's balance when you hold its orientation but not the account — the projection
    /// that groups many accounts at once. Where you have the account, ask it: it cannot be given
    /// the wrong orientation.
    /// </summary>
    public static decimal AccountBalance(NormalBalance normal, IEnumerable<JournalLine> lines)
    {
        var (debits, credits) = TrialBalance(lines);
        return normal == NormalBalance.Debit ? debits - credits : credits - debits;
    }
}
