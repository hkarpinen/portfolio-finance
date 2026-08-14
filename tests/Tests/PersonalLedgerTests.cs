using Finance.Application.Managers;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// A person's own book. The point of every one of these is that a card's balance is the ledger's
/// answer and not a number anybody stored.
/// </summary>
public class PersonalLedgerTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static OpenDebtAccountCommand Card(decimal openingBalance, decimal apr = 24.99m) => new(
        CallerUserId: User,
        Name: "Visa",
        Currency: "USD",
        AnnualPercentageRate: apr,
        OpeningBalance: openingBalance,
        AsOf: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        CreditLimit: 5000m,
        StatementDayOfMonth: 14,
        PaymentDueDayOfMonth: 8,
        MinimumPayment: 35m);

    [Fact]
    public async Task OpenDebtAccount_OpensTheUsersLedgerWhenTheyHaveNone()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        await manager.OpenDebtAccountAsync(Card(1200m));

        var ledger = await repo.GetLedgerByOwnerAsync(AccountingEntity.Person(User));
        Assert.NotNull(ledger);
        Assert.Equal(AccountingEntity.Person(User), ledger!.Owner);
    }

    [Fact]
    public async Task OpenDebtAccount_RecordsTheCardAsALiability()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        var accountId = await manager.OpenDebtAccountAsync(Card(1200m));

        var ledger = (await repo.GetLedgerByOwnerAsync(AccountingEntity.Person(User)))!;
        var accounts = await repo.GetAccountsAsync(ledger.Id);
        var card = accounts.Single(a => a.Id.Value == accountId);

        // Liability, so a purchase CREDITS it and a payment DEBITS it — no special-casing needed
        // anywhere in the engine.
        Assert.Equal(AccountType.Liability, card.AccountType);
        Assert.Equal(NormalBalance.Credit, card.NormalBalance);
    }

    [Fact]
    public async Task OpenDebtAccount_PostsTheOpeningBalanceRatherThanStoringIt()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        var accountId = await manager.OpenDebtAccountAsync(Card(1200m));

        var entry = Assert.Single(repo.JournalEntries);
        Assert.Equal(0m, entry.Postings.Sum(p => p.SignedAmount));

        // Owing 1,200 means the liability is CREDITED; opening-balance equity takes the debit.
        var onCard = entry.Postings.Single(p => p.AccountId.Value == accountId);
        Assert.Equal(EntryDirection.Credit, onCard.Direction);
        Assert.Equal(1200m, onCard.Amount.Amount);
    }

    [Fact]
    public async Task OpenDebtAccount_BalanceIsDerivedFromThePostings()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        var accountId = await manager.OpenDebtAccountAsync(Card(1200m));

        var postings = repo.JournalEntries
            .SelectMany(e => e.Postings)
            .Where(p => p.AccountId.Value == accountId);

        Assert.Equal(1200m, LedgerMath.AccountBalance(NormalBalance.Credit, postings));
    }

    [Fact]
    public async Task OpenDebtAccount_ANewCardWithNothingOwedPostsNothing()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        await manager.OpenDebtAccountAsync(Card(0m));

        // A zero entry would not validate, and an account with no postings already reads as zero.
        Assert.Empty(repo.JournalEntries);
    }

    [Fact]
    public async Task OpenDebtAccount_KeepsTheTermsBesideTheAccount()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        var accountId = await manager.OpenDebtAccountAsync(Card(1200m));

        var terms = Assert.Single(await repo.GetDebtTermsForUserAsync(User));
        Assert.Equal(accountId, terms.AccountId.Value);
        Assert.Equal(24.99m, terms.AnnualPercentageRate);
        Assert.Equal(5000m, terms.CreditLimit);
        Assert.Equal(3800m, terms.HeadroomAgainst(1200m));
    }

    [Fact]
    public async Task OpenDebtAccount_ASecondCardSharesTheOneLedger()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        await manager.OpenDebtAccountAsync(Card(1200m));
        await manager.OpenDebtAccountAsync(Card(300m) with { Name = "Amex" });

        var ledger = (await repo.GetLedgerByOwnerAsync(AccountingEntity.Person(User)))!;
        var accounts = await repo.GetAccountsAsync(ledger.Id);

        // Two cards. Counting liabilities outright would also catch the seeded payable, which
        // every book gets whoever owns it.
        Assert.Equal(2, accounts.Count(a => a.Code.StartsWith("2000:")));
        // Seeded once, not per card.
        Assert.Single(accounts.Where(a => a.Code == Chart.OpeningBalanceCode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void DebtTerms_RefusesARateThatIsNotOne(decimal apr) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DebtTerms.For(Card(), apr));

    [Fact]
    public void DebtTerms_RefuseAnAccountNobodyLentYouAnythingOn()
    {
        var cash = Chart.OpenCashAccount(LedgerId.New(), Guid.NewGuid(), "Checking");
        var payable = Chart.Payable(LedgerId.New()).Open();

        // A rate on money you hold, or on the shared payable, is not wrong-looking data —
        // it is meaningless data, so the pairing is refused rather than stored.
        Assert.Throws<InvalidOperationException>(() => DebtTerms.For(cash, 24.99m));
        Assert.Throws<InvalidOperationException>(() => DebtTerms.For(payable, 24.99m));
    }

    [Fact]
    public void DebtTerms_MonthlyRateIsTheAnnualOneOverTwelve()
    {
        var terms = DebtTerms.For(Card(), 24m);

        Assert.Equal(0.02m, terms.MonthlyRate);
    }

    [Fact]
    public void DebtTerms_ALoanHasNoHeadroom()
    {
        var loan = DebtTerms.For(Card(), 6.5m, creditLimit: null);

        Assert.Null(loan.HeadroomAgainst(10_000m));
    }

    /// A real card account: terms only mean anything against something borrowed.
    private static Account Card() =>
        Chart.OpenDebtAccount(LedgerId.New(), Guid.NewGuid(), "Visa");

    private static Charge Groceries(decimal amount = 45m, Guid? fundedBy = null) =>
        Charge.CreateOwn(UserId.Create(User), "Groceries", Money.Create(amount, "USD"),
            ChargeCategory.Groceries, new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            fundingAccountId: fundedBy);

    private sealed class ChargeStore : Finance.Application.Repositories.IChargeRepository
    {
        public List<Charge> Items { get; } = new();
        public Task AddAsync(Charge c, CancellationToken ct = default) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Charge c, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Charge c, CancellationToken ct = default) { Items.Remove(c); return Task.CompletedTask; }
        public Task<Charge?> GetByIdAsync(ChargeId id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<Charge>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Charge>>(
                Items.Where(c => c.Owner == AccountingEntity.Person(userId) && c.IsActive && c.OccurrenceDate.Date <= asOf.Date).ToList());
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(UserId u, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task PersonalCharge_IncursAPayable_RatherThanSettlingItself()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);

        var entry = Assert.Single(repo.JournalEntries);
        Assert.Equal(0m, entry.Postings.Sum(p => p.SignedAmount));
        Assert.Equal(Chart.ExpenseCode("groceries"),
            repo.CodeOf(entry.Postings.Single(p => p.Direction == EntryDirection.Debit).AccountId));
        // Owed until settled. Crediting the funding account here would collapse the cost and its
        // payment into one movement, leaving "has this been paid" with nothing to read.
        Assert.Equal(ChartCodes.VendorPayable,
            repo.CodeOf(entry.Postings.Single(p => p.Direction == EntryDirection.Credit).AccountId));
    }

    [Fact]
    public async Task PayingWithACard_ClearsTheCompanyAndMovesTheDebtToTheCard()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);

        var cardId = await manager.OpenDebtAccountAsync(Card(0m));
        var charge = Groceries(fundedBy: cardId);
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, cardId, DateTime.UtcNow);

        var postings = repo.JournalEntries.SelectMany(e => e.Postings).ToList();

        // The company is paid — the payable nets to nothing.
        Assert.Equal(0m, LedgerMath.AccountBalance(
            NormalBalance.Credit,
            postings.Where(p => repo.CodeOf(p.AccountId) == ChartCodes.VendorPayable)));

        // And the 45 is now owed to the card issuer instead, which is what happened.
        Assert.Equal(45m, LedgerMath.AccountBalance(
            NormalBalance.Credit, postings.Where(p => p.AccountId.Value == cardId)));
    }

    [Fact]
    public async Task PayingFromCash_SettlesTheSameWay_OnlyTheFundingDiffers()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, null, DateTime.UtcNow);

        var postings = repo.JournalEntries.SelectMany(e => e.Postings).ToList();

        Assert.Equal(0m, LedgerMath.AccountBalance(
            NormalBalance.Credit,
            postings.Where(p => repo.CodeOf(p.AccountId) == ChartCodes.VendorPayable)));
        // Cash is an asset, so paying out leaves it 45 down.
        Assert.Equal(-45m, LedgerMath.AccountBalance(
            NormalBalance.Debit,
            postings.Where(p => repo.CodeOf(p.AccountId) == Chart.CashCode)));
    }

    [Fact]
    public async Task RecordPersonalPayment_IsIdempotent()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, null, DateTime.UtcNow);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, null, DateTime.UtcNow);

        Assert.Equal(2, repo.JournalEntries.Count);
    }

    [Fact]
    public async Task PersonalCharge_IsConvergent_SoARedeliveryWritesNothing()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.ConvergePersonalChargeAsync(charge.Id.Value);

        Assert.Single(repo.JournalEntries);
    }

    [Fact]
    public async Task PersonalCharge_NeverTouchesAGroupLedger()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);

        Assert.Null(await repo.GetLedgerByOwnerAsync(AccountingEntity.Household(User)));
        Assert.NotNull(await repo.GetLedgerByOwnerAsync(AccountingEntity.Person(User)));
    }

    [Fact]
    public async Task ReversingAPayment_LeavesThePayableOwedAgain()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, null, DateTime.UtcNow);
        await manager.ReversePersonalPaymentAsync(charge.Id.Value);

        var payable = repo.JournalEntries.SelectMany(e => e.Postings)
            .Where(p => repo.CodeOf(p.AccountId) == ChartCodes.VendorPayable);

        // Owed once more — the cost never went away, only the settlement did.
        Assert.Equal(45m, LedgerMath.AccountBalance(NormalBalance.Credit, payable));
    }

    [Fact]
    public async Task ReversingAPayment_KeepsTheOriginalOnTheRecord()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.RecordPersonalPaymentAsync(charge.Id.Value, null, DateTime.UtcNow);
        await manager.ReversePersonalPaymentAsync(charge.Id.Value);

        // Incurred, settled, unsettled — three entries. The money did move and then move back,
        // which is two facts, not an erasure.
        Assert.Equal(3, repo.JournalEntries.Count);
        Assert.Single(repo.JournalEntries.Where(e => e.ReversalOfEntryId is not null));
    }

    [Fact]
    public async Task ReversingAPayment_IsANoOpWhenNothingWasSettled()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);
        await manager.ReversePersonalPaymentAsync(charge.Id.Value);

        Assert.Single(repo.JournalEntries);
    }

    [Fact]
    public void RecordPersonalPayment_RefusesASharedCharge()
    {
        var shared = Charge.Create(
            AccountingEntity.Household(GroupId.Create(Guid.NewGuid())), UserId.Create(User), "Rent", Money.Create(900m, "USD"),
            ChargeCategory.Rent, DateTime.UtcNow, null, payerUserId: User, fundingSource: FundingSource.PayerMember);

        // A shared cost is settled through its allocations — one member paying does not clear it.
        Assert.Throws<InvalidOperationException>(() => shared.RecordPersonalPayment(null, DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => shared.ReversePersonalPayment());
    }
}
