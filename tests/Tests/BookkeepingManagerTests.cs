using Finance.Application.Managers;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;
using Infrastructure.Queries;

namespace Tests;

// A settlement (and a vendor payment) posts against the funding account the expense's FundingSource
// dictates: PayerMember → the payer's Member account, GroupCash → the shared Cash pool.
public class BookkeepingManagerTests
{
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Debtor = Guid.NewGuid();
    private static readonly Guid Expense = Guid.NewGuid();
    private static readonly Guid Share = Guid.NewGuid();

    private static BookkeepingManager NewManager(out FakeLedgerRepository repo)
    {
        repo = new FakeLedgerRepository();
        // The direct ledger-journalLine methods never touch the expense/share repos — only the convergence
        // wrappers do — so the nulls below are never dereferenced.
        return new BookkeepingManager(repo, null!, null!, null!);
    }

    private static RecordSettlementCommand Settlement(FundingSource funding) => new(
        Group, Expense, Share, FromUserId: Debtor, ToUserId: Payer,
        Amount: 40m, Currency: "USD",
        Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
        Source: LedgerSources.Settlement(Expense, DateTime.UtcNow.Date, Debtor),
        FundingSource: funding);

    [Fact]
    public async Task RecordSettlement_PayerMember_PostsDrPayer_CrDebtor()
    {
        var manager = NewManager(out var repo);

        await manager.RecordSettlementAsync(Settlement(FundingSource.PayerMember));

        var entry = Assert.Single(repo.JournalEntries);
        var debit = entry.JournalLines.Single(p => p.Direction == EntryDirection.Debit);
        var credit = entry.JournalLines.Single(p => p.Direction == EntryDirection.Credit);

        Assert.Equal(Chart.MemberCode(Payer), repo.CodeOf(debit.AccountId));
        Assert.Equal(Chart.MemberCode(Debtor), repo.CodeOf(credit.AccountId));
        Assert.Equal(40m, debit.Amount.Amount);
    }

    [Fact]
    public async Task RecordSettlement_GroupCash_PostsDrCash_CrDebtor()
    {
        var manager = NewManager(out var repo);

        await manager.RecordSettlementAsync(Settlement(FundingSource.GroupCash));

        var entry = Assert.Single(repo.JournalEntries);
        var debit = entry.JournalLines.Single(p => p.Direction == EntryDirection.Debit);
        var credit = entry.JournalLines.Single(p => p.Direction == EntryDirection.Credit);

        Assert.Equal(Chart.CashCode, repo.CodeOf(debit.AccountId));
        Assert.Equal(Chart.MemberCode(Debtor), repo.CodeOf(credit.AccountId));
    }

    [Fact]
    public async Task RecordVendorPayment_MirrorsExpense_Funding()
    {
        var payerManager = NewManager(out var payerRepo);
        await payerManager.RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            Group, Expense, Total: 100m, Currency: "USD",
            FundingSource.PayerMember, PaidByUserId: Payer,
            Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
            Source: LedgerSources.VendorPayment(Expense, DateTime.UtcNow.Date)));

        var payerEntry = Assert.Single(payerRepo.JournalEntries);
        // Dr Vendor Payable / Cr Member:payer — the payer fronted it.
        Assert.Equal(Chart.PayableCode,
            payerRepo.CodeOf(payerEntry.JournalLines.Single(p => p.Direction == EntryDirection.Debit).AccountId));
        Assert.Equal(Chart.MemberCode(Payer),
            payerRepo.CodeOf(payerEntry.JournalLines.Single(p => p.Direction == EntryDirection.Credit).AccountId));

        var poolManager = NewManager(out var poolRepo);
        await poolManager.RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            Group, Expense, Total: 100m, Currency: "USD",
            FundingSource.GroupCash, PaidByUserId: null,
            Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
            Source: LedgerSources.VendorPayment(Expense, DateTime.UtcNow.Date)));

        var poolEntry = Assert.Single(poolRepo.JournalEntries);
        // Dr Vendor Payable / Cr Cash — paid from the pot.
        Assert.Equal(Chart.CashCode,
            poolRepo.CodeOf(poolEntry.JournalLines.Single(p => p.Direction == EntryDirection.Credit).AccountId));
    }

    [Fact]
    public async Task SyncExpenseAccrual_CorrectionReversesInThePeriodItCorrects()
    {
        var manager = NewManager(out var repo);
        var july = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await manager.SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            Group, Expense, "Rent", "Housing", 1000m, "USD", july));
        await manager.SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            Group, Expense, "Rent", "Housing", 1100m, "USD", july));

        // Reversal dated August with a re-post dated July left July carrying 1,000 + 1,100 and a
        // balance sheet at 31 July reporting 2,100.
        var asAtJulyEnd = repo.JournalEntries
            .Where(e => e.Date <= new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc))
            .SelectMany(e => e.JournalLines)
            .Sum(p => p.SignedAmount);

        Assert.Equal(0m, asAtJulyEnd);
        Assert.All(repo.JournalEntries, e => Assert.Equal(july, e.Date));
    }

    [Fact]
    public async Task SyncExpenseAccrual_CorrectionLeavesTheRightAmountOnTheBooks()
    {
        var manager = NewManager(out var repo);
        var july = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await manager.SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            Group, Expense, "Rent", "Housing", 1000m, "USD", july));
        await manager.SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            Group, Expense, "Rent", "Housing", 1100m, "USD", july));

        // Original + reversal + re-post: the payable nets to the corrected figure.
        var lines = repo.JournalEntries.SelectMany(e => e.JournalLines)
            .Where(p => repo.CodeOf(p.AccountId) == Chart.PayableCode);

        Assert.Equal(1100m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, lines));
    }

    [Fact]
    public async Task RecordSettlement_AttributesTheEntryToWhoeverPaid()
    {
        var manager = NewManager(out var repo);

        await manager.RecordSettlementAsync(Settlement(FundingSource.PayerMember));

        var entry = Assert.Single(repo.JournalEntries);
        // The provenance columns say WHICH share; this says who acted on it.
        Assert.Equal(Debtor, entry.PostedByUserId);
    }

    [Fact]
    public async Task SyncExpenseAccrual_AttributesTheEntryToWhoeverEnteredTheBill()
    {
        var manager = NewManager(out var repo);
        var owner = Guid.NewGuid();

        await manager.SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            Group, Expense, "Rent", "Rent", 1000m, "USD",
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), PostedByUserId: owner));

        Assert.Equal(owner, Assert.Single(repo.JournalEntries).PostedByUserId);
    }

    internal sealed class FakeLedgerRepository : ILedgerRepository
    {
        private readonly List<Ledger> _ledgers = new();
        private readonly List<Account> _accounts = new();
        public List<JournalEntry> JournalEntries { get; } = new();

        public string CodeOf(AccountId id) => _accounts.Single(a => a.Id == id).Code;

        public Task<Ledger?> GetLedgerByOwnerAsync(AccountingEntity owner, CancellationToken ct = default)
            => Task.FromResult(_ledgers.FirstOrDefault(l => l.Owner == owner));

        public Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default) { _ledgers.Add(ledger); return Task.CompletedTask; }

        public Task<Ledger> GetOrOpenLedgerAsync(
            AccountingEntity owner, string currency, CancellationToken ct = default)
        {
            var existing = _ledgers.FirstOrDefault(l => l.Owner == owner);
            if (existing is not null) return Task.FromResult(existing);

            var ledger = Ledger.Open(owner, currency);
            _ledgers.Add(ledger);
            _accounts.AddRange(Chart.StandardAccounts(ledger.Id));
            return Task.FromResult(ledger);
        }

        public Task<Account> GetOrOpenAccountAsync(LedgerId ledgerId, AccountSpec spec, CancellationToken ct = default)
        {
            var existing = _accounts.FirstOrDefault(a => a.LedgerId == ledgerId && a.Code == spec.Code);
            if (existing is not null) return Task.FromResult(existing);
            var account = spec.Open();
            _accounts.Add(account);
            return Task.FromResult(account);
        }

        public Task<Account?> GetAccountAsync(AccountId accountId, CancellationToken ct = default)
            => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == accountId));

        public Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.LedgerId == ledgerId).ToList());

        public Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default)
            => Task.FromResult(_accounts.FirstOrDefault(a => a.LedgerId == ledgerId && a.Code == code));

        public Task AddAccountAsync(Account account, CancellationToken ct = default) { _accounts.Add(account); return Task.CompletedTask; }

        public List<DebtTerms> DebtTerms { get; } = new();
        public Task AddDebtTermsAsync(DebtTerms terms, CancellationToken ct = default) { DebtTerms.Add(terms); return Task.CompletedTask; }
        // Whose debt it is comes from the ledger the account sits in — the same join the real
        // repository does.
        public Task<IReadOnlyList<DebtTerms>> GetDebtTermsForUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DebtTerms>>(DebtTerms.Where(t =>
                _accounts.Any(a => a.Id == t.AccountId
                    && _ledgers.Any(l => l.Id == a.LedgerId
                        && l.Owner == AccountingEntity.Person(userId)))).ToList());

        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default) { JournalEntries.Add(entry); return Task.CompletedTask; }

        public Task<bool> ConvergeAsync(JournalEntry candidate, bool postOnce = false, CancellationToken ct = default)
        {
            var inEffect = JournalEntries
                .Where(e => e.LedgerId == candidate.LedgerId && e.Source == candidate.Source)
                .ToList().InEffect();
            if (postOnce && inEffect.Count > 0) return Task.FromResult(false);

            var plan = ConvergencePlan.For(candidate, inEffect);
            if (plan.AlreadyThere) return Task.FromResult(false);

            foreach (var stale in plan.Reverse) JournalEntries.Add(stale.Reverse(stale.Date));
            if (plan.Post is { } entry) JournalEntries.Add(entry);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalEntry>>(JournalEntries.Where(e => e.LedgerId == ledgerId && e.Source == source).ToList());

        public Task<IReadOnlyList<JournalEntry>> GetEntriesByExpenseAsync(LedgerId ledgerId, Guid expenseId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalEntry>>(JournalEntries.Where(e => e.LedgerId == ledgerId && e.SourceExpenseId == expenseId).ToList());

        public Task<IReadOnlyList<JournalLine>> GetJournalLinesByAccountAsync(AccountId accountId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalLine>>(JournalEntries.SelectMany(e => e.JournalLines).Where(p => p.AccountId == accountId).ToList());

        public Task<IReadOnlyList<JournalLine>> GetJournalLinesByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalLine>>(JournalEntries.Where(e => e.LedgerId == ledgerId).SelectMany(e => e.JournalLines).ToList());

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}