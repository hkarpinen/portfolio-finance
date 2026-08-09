using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// Turns a business event into balanced journal-entry drafts. Pure — no I/O, and
/// account ids are resolved by the caller.
/// </summary>
public interface IJournalizingEngine
{
    /// <summary>
    /// Journalize a charge. Postconditions: every returned draft is balanced
    /// (Σ debits == Σ credits); after posting, the payer's member account nets to what
    /// the group owes them (fronted − borne) and each other member to what they owe.
    /// </summary>
    IReadOnlyList<JournalEntryDraft> JournalizeCharge(ChargeAllocationContext context);

    /// <summary>
    /// ACCRUAL basis: incurred and owed, but not yet funded. Postconditions — every draft
    /// balances; Vendor Payable equals the total owed; each member's account reflects the
    /// share they bear. Who actually pays the vendor is a later transfer.
    /// </summary>
    IReadOnlyList<JournalEntryDraft> JournalizeAccrual(AccrualContext context);

    /// <summary>Journalize a transfer: a balanced 1↔1 move between two accounts (settlement,
    /// contribution, payment, payoff). Postcondition: balanced (a single Dr and Cr of equal amount).</summary>
    JournalEntryDraft JournalizeTransfer(TransferContext context);
}

/// <summary>
/// Cash basis: two balanced entries, so member equity separates who FRONTED a cost from
/// who BORE it.
/// </summary>
internal sealed class CashBasisJournalizingEngine : IJournalizingEngine
{
    public IReadOnlyList<JournalEntryDraft> JournalizeCharge(ChargeAllocationContext c)
    {
        var currency = c.Total.Currency;

        // Entry 1 — cost incurred, paid from one account (a member's pocket, or a shared
        // pool/card — the engine doesn't care which):
        //   Dr Expense:{cat} (total)   Cr FundingAccount (total)
        var incurred = new JournalEntryDraft(
            c.Date, $"{c.Description} — incurred", c.Source,
            new[]
            {
                PostingLine.Debit(c.ExpenseAccount, c.Total),
                PostingLine.Credit(c.FundingAccount, c.Total),
            });

        // Entry 2 — cost borne by members per share; the funding account absorbs any
        // unallocated remainder (Total − Σ shares). When it is a member that is their own
        // implicit share; when it is a pool the shares sum to Total so remainder is 0 and no
        // line is added:
        //   Dr Member:{each} (share) [+ Dr FundingAccount (remainder)]   Cr Expense:{cat} (total)
        var sharesTotal = c.Shares.Aggregate(0m, (sum, s) => sum + s.Amount.Amount);
        var remainder = c.Total.Amount - sharesTotal;

        var lines = new List<PostingLine>(c.Shares.Count + 2);
        foreach (var s in c.Shares)
            lines.Add(PostingLine.Debit(s.MemberAccount, s.Amount));
        if (remainder > 0m)
            lines.Add(PostingLine.Debit(c.FundingAccount, Money.Create(remainder, currency)));
        lines.Add(PostingLine.Credit(c.ExpenseAccount, c.Total));

        var allocated = new JournalEntryDraft(
            c.Date, $"{c.Description} — allocated", c.Source, lines);

        return new[] { incurred, allocated };
    }

    public IReadOnlyList<JournalEntryDraft> JournalizeAccrual(AccrualContext c)
    {
        var currency = c.Total.Currency;

        // Entry 1 — cost incurred, owed to the vendor (not yet funded):
        //   Dr Expense:{cat} (total)   Cr Vendor Payable (total)
        var incurred = new JournalEntryDraft(
            c.Date, $"{c.Description} — incurred", c.Source,
            new[]
            {
                PostingLine.Debit(c.ExpenseAccount, c.Total),
                PostingLine.Credit(c.VendorPayableAccount, c.Total),
            });

        // Entry 2 — cost borne by members per share. There is no funding account at accrual time,
        // so the cash-basis remainder-to-funding line is deliberately omitted: any unallocated
        // remainder (Total − Σ shares) simply stays debited on the Expense account (household-borne).
        // No shares yet → no allocation entry at all (a zero-amount credit would not validate).
        var sharesTotal = c.Shares.Aggregate(0m, (sum, s) => sum + s.Amount.Amount);
        if (sharesTotal == 0m)
            return new[] { incurred };

        var lines = new List<PostingLine>(c.Shares.Count + 1);
        foreach (var s in c.Shares)
            lines.Add(PostingLine.Debit(s.MemberAccount, s.Amount));
        lines.Add(PostingLine.Credit(c.ExpenseAccount, Money.Create(sharesTotal, currency)));

        var allocated = new JournalEntryDraft(
            c.Date, $"{c.Description} — allocated", c.Source, lines);

        return new[] { incurred, allocated };
    }

    public JournalEntryDraft JournalizeTransfer(TransferContext c)
    {
        // A balanced 1↔1 move: debit one account, credit the other. Caller chose the direction.
        return new JournalEntryDraft(
            c.ValueDate, c.Description, c.Source,
            new[]
            {
                PostingLine.Debit(c.DebitAccount, c.Amount),
                PostingLine.Credit(c.CreditAccount, c.Amount),
            });
    }
}
