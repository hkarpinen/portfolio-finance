using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

public class JournalizingEngineTests
{
    private static readonly LedgerId L = LedgerId.New();
    private static readonly AccountId Expense = AccountId.New();
    private static readonly AccountId Cash = AccountId.New();
    private static readonly AccountId Hank = AccountId.New();
    private static readonly AccountId Bob = AccountId.New();
    private static readonly AccountId VendorPayable = AccountId.New();
    private static Money Usd(decimal a) => Money.Create(a, "USD");

    private readonly IJournalizingEngine _engine = new CashBasisJournalizingEngine();

    private static List<JournalLine> Post(LedgerId ledger, IEnumerable<JournalEntryDraft> drafts) =>
        drafts.SelectMany(d => JournalEntry.Post(ledger, d.Date, d.Description, d.Lines, d.Source).JournalLines).ToList();

    [Fact]
    public void JournalizeExpense_AllDraftsBalance()
    {
        var ctx = new ExpenseShareContext(
            Expense, Hank,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123");

        var drafts = _engine.JournalizeExpense(ctx);

        Assert.Equal(2, drafts.Count);
        foreach (var d in drafts)
            Assert.True(LedgerMath.IsBalanced(JournalEntry.Post(L, d.Date, d.Description, d.Lines, d.Source).JournalLines));
    }

    [Fact]
    public void JournalizeExpense_MemberBalances_PayerOwedForFrontedShare()
    {
        // Rent $1,000, payer Hank, explicit shares Hank $700 / Bob $300.
        var ctx = new ExpenseShareContext(
            Expense, Hank,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123");

        var lines = Post(L, _engine.JournalizeExpense(ctx));

        // Member equity is credit-normal. Hank fronted 1000, bore 700 → +300 (group owes Hank).
        Assert.Equal(300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Hank)));
        // Bob bore 300, fronted 0 → −300 (Bob owes).
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Bob)));
        // Nominal expense account is recorded then allocated → nets to 0 (cash-basis, no period accrual).
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Expense)));
        Assert.True(LedgerMath.IsBalanced(lines));
    }

    [Fact]
    public void JournalizeExpense_PayerAbsorbsUnallocatedRemainder()
    {
        // Only Bob has an explicit $300 share of a $1,000 bill — Hank (payer) absorbs $700.
        var ctx = new ExpenseShareContext(
            Expense, Hank,
            new[] { new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123");

        var lines = Post(L, _engine.JournalizeExpense(ctx));

        Assert.Equal(300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Hank)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Bob)));
        Assert.True(LedgerMath.IsBalanced(lines));
    }

    [Fact]
    public void JournalizeExpense_PaidFromSharedPool_MembersOweTheirShare()
    {
        // SAME engine, funding account = Cash (a shared pool, not a member). All members'
        // shares sum to the total — there is no funder-member, so no remainder line.
        var lines = Post(L, _engine.JournalizeExpense(new ExpenseShareContext(
            Expense, FundingAccount: Cash,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:pool")));

        // The pool fronted the whole bill (asset down 1000); each member owes their share.
        Assert.Equal(-1000m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Cash)));
        Assert.Equal(-700m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Hank)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Bob)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Expense)));
        Assert.True(LedgerMath.IsBalanced(lines));

        // Both members contribute their share INTO the pool (collect-first) — drains Cash to 0.
        // A contribution debits Cash (pool fills) and credits the member (their debt clears).
        var contribHank = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: Cash, CreditAccount: Hank, Amount: Usd(700),
            ValueDate: DateTime.UtcNow, Description: "Hank contributes", Source: "c:h"));
        var contribBob = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: Cash, CreditAccount: Bob, Amount: Usd(300),
            ValueDate: DateTime.UtcNow, Description: "Bob contributes", Source: "c:b"));

        var all = lines
            .Concat(JournalEntry.Post(L, contribHank.Date, contribHank.Description, contribHank.Lines, contribHank.Source).JournalLines)
            .Concat(JournalEntry.Post(L, contribBob.Date, contribBob.Description, contribBob.Lines, contribBob.Source).JournalLines)
            .ToList();

        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, all.Where(p => p.AccountId == Cash)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Hank)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Bob)));
        Assert.True(LedgerMath.IsBalanced(all));
    }

    [Fact]
    public void JournalizeReimbursement_SettlesBothMembers()
    {
        // Start from the owed position, then Bob reimburses Hank $300.
        var expenseLines = Post(L, _engine.JournalizeExpense(new ExpenseShareContext(
            Expense, Hank,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123")));

        // Bob settles Hank: debit Hank (the funding account he fronted from), credit Bob (debtor).
        var reimb = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: Hank, CreditAccount: Bob, Amount: Usd(300),
            ValueDate: DateTime.UtcNow, Description: "Bob reimburses Hank", Source: "reimb:1"));
        var reimbJournalLines = JournalEntry.Post(L, reimb.Date, reimb.Description, reimb.Lines, reimb.Source).JournalLines;

        Assert.True(LedgerMath.IsBalanced(reimbJournalLines));

        var all = expenseLines.Concat(reimbJournalLines).ToList();
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Hank)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Bob)));
    }


    [Fact]
    public void JournalizeAccrual_OwesVendor_AndAllocatesToMembers()
    {
        // Accrual: a new bill is incurred and OWED to the vendor (not yet funded).
        var drafts = _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123"));

        Assert.Equal(2, drafts.Count);
        var lines = Post(L, drafts);

        // We owe the vendor the full total (Vendor Payable is credit-normal).
        Assert.Equal(1000m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == VendorPayable)));
        // Each member bears their share (credit-normal equity goes negative).
        Assert.Equal(-700m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Hank)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Bob)));
        // Fully allocated → the nominal expense nets to 0.
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Expense)));
        Assert.True(LedgerMath.IsBalanced(lines));
    }

    [Fact]
    public void JournalizeAccrual_NoShares_EmitsOnlyTheIncurredEntry()
    {
        // A bill with no shares yet: only the vendor liability is recorded. The share
        // entry is omitted entirely — a zero-amount credit would fail JournalEntry validation.
        var drafts = _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            Array.Empty<MemberShare>(),
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123"));

        var lines = Post(L, drafts);

        Assert.Single(drafts);
        Assert.Equal(1000m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == VendorPayable)));
        Assert.Equal(1000m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Expense)));
        Assert.True(LedgerMath.IsBalanced(lines));
    }

    [Fact]
    public void JournalizeAccrual_UnallocatedRemainder_StaysOnExpense_NotOnAnyMember()
    {
        // Only Bob has an explicit $300 share of a $1,000 bill — the rest is unallocated.
        var lines = Post(L, _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            new[] { new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123")));

        Assert.Equal(1000m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == VendorPayable)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Bob)));
        // No funding account exists at accrual time → the remainder stays on Expense (household-borne);
        // crucially no member is over-debited (Hank, who has no share, stays at 0).
        Assert.Equal(700m, LedgerMath.AccountBalance(NormalBalance.Debit, lines.Where(p => p.AccountId == Expense)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, lines.Where(p => p.AccountId == Hank)));
        Assert.True(LedgerMath.IsBalanced(lines));
    }

    [Fact]
    public void AccrualThenVendorPaid_PayerMember_MatchesCashBasisEndState_ThenSettlesToZero()
    {
        var accrual = Post(L, _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:123")));

        // Hank fronts the vendor: Dr Vendor Payable / Cr Hank.
        var pay = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: VendorPayable, CreditAccount: Hank, Amount: Usd(1000),
            ValueDate: DateTime.UtcNow, Description: "Vendor payment", Source: "vendorpayment:123"));
        var paid = accrual.Concat(JournalEntry.Post(L, pay.Date, pay.Description, pay.Lines, pay.Source).JournalLines).ToList();

        // Vendor cleared; end-state identical to today's cash-basis: group owes Hank 300, Bob owes 300.
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, paid.Where(p => p.AccountId == VendorPayable)));
        Assert.Equal(300m, LedgerMath.AccountBalance(NormalBalance.Credit, paid.Where(p => p.AccountId == Hank)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, paid.Where(p => p.AccountId == Bob)));
        Assert.True(LedgerMath.IsBalanced(paid));

        // Bob settles → everyone nets to zero.
        var settle = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: Hank, CreditAccount: Bob, Amount: Usd(300),
            ValueDate: DateTime.UtcNow, Description: "Settlement", Source: "settlement:123"));
        var settled = paid.Concat(JournalEntry.Post(L, settle.Date, settle.Description, settle.Lines, settle.Source).JournalLines).ToList();
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, settled.Where(p => p.AccountId == Hank)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, settled.Where(p => p.AccountId == Bob)));
        Assert.True(LedgerMath.IsBalanced(settled));
    }

    [Fact]
    public void CollectFirst_PerShare_SettleIntoPot_ThenOwnerPaysVendor_AllZero()
    {
        var all = new List<JournalLine>();
        void PostT(JournalEntryDraft d) => all.AddRange(JournalEntry.Post(L, d.Date, d.Description, d.Lines, d.Source).JournalLines);
        JournalEntryDraft T(AccountId dr, AccountId cr, decimal amt, string src) =>
            _engine.JournalizeTransfer(new TransferContext(
                DebitAccount: dr, CreditAccount: cr, Amount: Usd(amt),
                ValueDate: DateTime.UtcNow, Description: "t", Source: src));

        // Bill incurred (owed to vendor); each share journaled per-share: Dr Member / Cr Expense.
        PostT(T(Expense, VendorPayable, 1000, "expense:c"));
        PostT(T(Hank, Expense, 500, "share:a1"));
        PostT(T(Bob, Expense, 500, "share:a2"));

        // Mid-state: members owe their share, the nominal expense nets to 0, we owe the vendor in full.
        Assert.Equal(-500m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Hank)));
        Assert.Equal(-500m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Bob)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, all.Where(p => p.AccountId == Expense)));
        Assert.Equal(1000m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == VendorPayable)));

        // Each member pays their share INTO the pot (Dr Cash / Cr Member); owner pays the vendor from
        // the pot (Dr Vendor Payable / Cr Cash).
        PostT(T(Cash, Hank, 500, "settlement:c:h"));
        PostT(T(Cash, Bob, 500, "settlement:c:b"));
        PostT(T(VendorPayable, Cash, 1000, "vendorpayment:c"));

        foreach (var acct in new[] { Hank, Bob, Expense, VendorPayable, Cash })
            Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == acct)));
        Assert.True(LedgerMath.IsBalanced(all));
    }

    [Fact]
    public void Expense_FullUnwind_ReversingEveryEntry_ReturnsAllAccountsToZero()
    {
        // The full lifecycle: accrual → Hank fronts the vendor → Bob settles his share.
        var entries = new List<JournalEntry>();
        void PostEntry(JournalEntryDraft d) =>
            entries.Add(JournalEntry.Post(L, d.Date, d.Description, d.Lines, d.Source));

        foreach (var d in _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:z")))
            PostEntry(d);
        PostEntry(_engine.JournalizeTransfer(new TransferContext(
            DebitAccount: VendorPayable, CreditAccount: Hank, Amount: Usd(1000),
            ValueDate: DateTime.UtcNow, Description: "Vendor payment", Source: "vendorpayment:z")));
        PostEntry(_engine.JournalizeTransfer(new TransferContext(
            DebitAccount: Hank, CreditAccount: Bob, Amount: Usd(300),
            ValueDate: DateTime.UtcNow, Description: "Settlement", Source: "settlement:z")));

        // Delete the bill → reverse EVERY entry tagged with it.
        var all = entries.SelectMany(e => e.JournalLines)
            .Concat(entries.SelectMany(e => e.Reverse(DateTime.UtcNow.Date).JournalLines))
            .ToList();

        foreach (var acct in new[] { VendorPayable, Hank, Bob, Expense })
            Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == acct)));
        Assert.True(LedgerMath.IsBalanced(all));
    }

    [Fact]
    public void AccrualThenVendorPaid_GroupCash_DrainsPot_MembersOwe()
    {
        var accrual = Post(L, _engine.JournalizeAccrual(new AccrualContext(
            Expense, VendorPayable,
            new[] { new MemberShare(Hank, Usd(700)), new MemberShare(Bob, Usd(300)) },
            Usd(1000), DateTime.UtcNow, "Rent", "expense:pool")));

        // The pot pays the vendor: Dr Vendor Payable / Cr Cash.
        var pay = _engine.JournalizeTransfer(new TransferContext(
            DebitAccount: VendorPayable, CreditAccount: Cash, Amount: Usd(1000),
            ValueDate: DateTime.UtcNow, Description: "Vendor payment", Source: "vendorpayment:pool"));
        var all = accrual.Concat(JournalEntry.Post(L, pay.Date, pay.Description, pay.Lines, pay.Source).JournalLines).ToList();

        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == VendorPayable)));
        // Pot fronted it (overdrawn until members contribute) — matches the pooled scenario.
        Assert.Equal(-1000m, LedgerMath.AccountBalance(NormalBalance.Debit, all.Where(p => p.AccountId == Cash)));
        Assert.Equal(-700m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Hank)));
        Assert.Equal(-300m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == Bob)));
        Assert.True(LedgerMath.IsBalanced(all));
    }

    [Fact]
    public void JournalizeExpense_Refuses_WhenSharesExceedTheTotal()
    {
        // Reachable by editing an expense DOWN after it has been split: the shares are
        // untouched, so the next journalization sees shares above the total.
        var context = new ExpenseShareContext(
            Expense, Cash,
            new[] { new MemberShare(Hank, Usd(50)), new MemberShare(Bob, Usd(50)) },
            Usd(60), DateTime.UtcNow, "Groceries", "expense:1");

        var error = Assert.Throws<InvalidOperationException>(() => _engine.JournalizeExpense(context));

        // Names the real problem. Left to fall through it reported "entry does not balance",
        // which is true and useless.
        Assert.Contains("exceed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JournalizeExpense_Allows_SharesEqualToTheTotal()
    {
        var context = new ExpenseShareContext(
            Expense, Cash,
            new[] { new MemberShare(Hank, Usd(30)), new MemberShare(Bob, Usd(30)) },
            Usd(60), DateTime.UtcNow, "Groceries", "expense:1");

        var lines = Post(L, _engine.JournalizeExpense(context));

        Assert.Equal(0m, lines.Sum(p => p.SignedAmount));
    }

    [Fact]
    public void JournalizeTransfer_Refuses_BothLegsOnOneAccount()
    {
        // Balanced and two-legged, so every check the journal makes would pass — and it
        // records a movement that never happened.
        var context = new TransferContext(Cash, Cash, Usd(25), DateTime.UtcNow, "Nowhere", "x");

        Assert.Throws<InvalidOperationException>(() => _engine.JournalizeTransfer(context));
    }
}
