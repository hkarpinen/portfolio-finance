using Finance.Application.Managers;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

// A settlement (and a vendor payment) posts against the funding account the charge's FundingSource
// dictates: PayerMember → the payer's Member account, GroupCash → the shared Cash pool.
public class BookkeepingManagerTests
{
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Debtor = Guid.NewGuid();
    private static readonly Guid Charge = Guid.NewGuid();
    private static readonly Guid Allocation = Guid.NewGuid();

    private static BookkeepingManager NewManager(out FakeLedgerRepository repo)
    {
        repo = new FakeLedgerRepository();
        // The direct ledger-posting methods never touch the charge/allocation repos — only the convergence
        // wrappers do — so the nulls below are never dereferenced.
        return new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);
    }

    private static RecordSettlementCommand Settlement(FundingSource funding) => new(
        Group, Charge, Allocation, FromUserId: Debtor, ToUserId: Payer,
        Amount: 40m, Currency: "USD",
        Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
        Source: LedgerSources.Settlement(Charge, DateTime.UtcNow.Date, Debtor),
        FundingSource: funding);

    [Fact]
    public async Task RecordSettlement_PayerMember_PostsDrPayer_CrDebtor()
    {
        var manager = NewManager(out var repo);

        await manager.RecordSettlementAsync(Settlement(FundingSource.PayerMember));

        var entry = Assert.Single(repo.JournalEntries);
        var debit = entry.Postings.Single(p => p.Direction == EntryDirection.Debit);
        var credit = entry.Postings.Single(p => p.Direction == EntryDirection.Credit);

        Assert.Equal(GroupChart.MemberCode(Payer), repo.CodeOf(debit.AccountId));
        Assert.Equal(GroupChart.MemberCode(Debtor), repo.CodeOf(credit.AccountId));
        Assert.Equal(40m, debit.Amount.Amount);
    }

    [Fact]
    public async Task RecordSettlement_GroupCash_PostsDrCash_CrDebtor()
    {
        var manager = NewManager(out var repo);

        await manager.RecordSettlementAsync(Settlement(FundingSource.GroupCash));

        var entry = Assert.Single(repo.JournalEntries);
        var debit = entry.Postings.Single(p => p.Direction == EntryDirection.Debit);
        var credit = entry.Postings.Single(p => p.Direction == EntryDirection.Credit);

        Assert.Equal(GroupChart.CashCode, repo.CodeOf(debit.AccountId));
        Assert.Equal(GroupChart.MemberCode(Debtor), repo.CodeOf(credit.AccountId));
    }

    [Fact]
    public async Task RecordVendorPayment_MirrorsCharge_Funding()
    {
        var payerManager = NewManager(out var payerRepo);
        await payerManager.RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            Group, Charge, Total: 100m, Currency: "USD",
            FundingSource.PayerMember, PaidByUserId: Payer,
            Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
            Source: LedgerSources.VendorPayment(Charge, DateTime.UtcNow.Date)));

        var payerEntry = Assert.Single(payerRepo.JournalEntries);
        // Dr Vendor Payable / Cr Member:payer — the payer fronted it.
        Assert.Equal(GroupChart.VendorPayableCode,
            payerRepo.CodeOf(payerEntry.Postings.Single(p => p.Direction == EntryDirection.Debit).AccountId));
        Assert.Equal(GroupChart.MemberCode(Payer),
            payerRepo.CodeOf(payerEntry.Postings.Single(p => p.Direction == EntryDirection.Credit).AccountId));

        var poolManager = NewManager(out var poolRepo);
        await poolManager.RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            Group, Charge, Total: 100m, Currency: "USD",
            FundingSource.GroupCash, PaidByUserId: null,
            Occurrence: DateTime.UtcNow.Date, ValueDate: DateTime.UtcNow.Date,
            Source: LedgerSources.VendorPayment(Charge, DateTime.UtcNow.Date)));

        var poolEntry = Assert.Single(poolRepo.JournalEntries);
        // Dr Vendor Payable / Cr Cash — paid from the pot.
        Assert.Equal(GroupChart.CashCode,
            poolRepo.CodeOf(poolEntry.Postings.Single(p => p.Direction == EntryDirection.Credit).AccountId));
    }

    internal sealed class FakeLedgerRepository : ILedgerRepository
    {
        private readonly List<Ledger> _ledgers = new();
        private readonly List<Account> _accounts = new();
        public List<JournalEntry> JournalEntries { get; } = new();

        public string CodeOf(AccountId id) => _accounts.Single(a => a.Id == id).Code;

        public Task<Ledger?> GetLedgerByOwnerAsync(LedgerOwnerType ownerType, Guid ownerId, CancellationToken ct = default)
            => Task.FromResult(_ledgers.FirstOrDefault(l => l.OwnerType == ownerType && l.OwnerId == ownerId));

        public Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default) { _ledgers.Add(ledger); return Task.CompletedTask; }

        public Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.LedgerId == ledgerId).ToList());

        public Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default)
            => Task.FromResult(_accounts.FirstOrDefault(a => a.LedgerId == ledgerId && a.Code == code));

        public Task AddAccountAsync(Account account, CancellationToken ct = default) { _accounts.Add(account); return Task.CompletedTask; }

        public List<DebtTerms> DebtTerms { get; } = new();
        public Task AddDebtTermsAsync(DebtTerms terms, CancellationToken ct = default) { DebtTerms.Add(terms); return Task.CompletedTask; }
        public Task<IReadOnlyList<DebtTerms>> GetDebtTermsForUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DebtTerms>>(DebtTerms.Where(t => t.UserId.Value == userId).ToList());

        public Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default) { JournalEntries.Add(entry); return Task.CompletedTask; }

        public Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalEntry>>(JournalEntries.Where(e => e.LedgerId == ledgerId && e.Source == source).ToList());

        public Task<IReadOnlyList<JournalEntry>> GetEntriesByChargeAsync(LedgerId ledgerId, Guid chargeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<JournalEntry>>(JournalEntries.Where(e => e.LedgerId == ledgerId && e.SourceChargeId == chargeId).ToList());

        public Task<IReadOnlyList<Posting>> GetPostingsByAccountAsync(AccountId accountId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Posting>>(JournalEntries.SelectMany(e => e.Postings).Where(p => p.AccountId == accountId).ToList());

        public Task<IReadOnlyList<Posting>> GetPostingsByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Posting>>(JournalEntries.Where(e => e.LedgerId == ledgerId).SelectMany(e => e.Postings).ToList());

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
