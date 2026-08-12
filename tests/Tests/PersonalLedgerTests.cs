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
        UserId: User,
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

        var ledger = await repo.GetLedgerByOwnerAsync(LedgerOwnerType.User, User);
        Assert.NotNull(ledger);
        Assert.Equal(LedgerOwnerType.User, ledger!.OwnerType);
    }

    [Fact]
    public async Task OpenDebtAccount_RecordsTheCardAsALiability()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), null!, null!);

        var accountId = await manager.OpenDebtAccountAsync(Card(1200m));

        var ledger = (await repo.GetLedgerByOwnerAsync(LedgerOwnerType.User, User))!;
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

        var ledger = (await repo.GetLedgerByOwnerAsync(LedgerOwnerType.User, User))!;
        var accounts = await repo.GetAccountsAsync(ledger.Id);

        Assert.Equal(2, accounts.Count(a => a.AccountType == AccountType.Liability));
        // Seeded once, not per card.
        Assert.Single(accounts.Where(a => a.Code == PersonalChart.OpeningBalanceCode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void DebtTerms_RefusesARateThatIsNotOne(decimal apr) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DebtTerms.For(AccountId.New(), UserId.Create(User), apr));

    [Fact]
    public void DebtTerms_MonthlyRateIsTheAnnualOneOverTwelve()
    {
        var terms = DebtTerms.For(AccountId.New(), UserId.Create(User), 24m);

        Assert.Equal(0.02m, terms.MonthlyRate);
    }

    [Fact]
    public void DebtTerms_ALoanHasNoHeadroom()
    {
        var loan = DebtTerms.For(AccountId.New(), UserId.Create(User), 6.5m, creditLimit: null);

        Assert.Null(loan.HeadroomAgainst(10_000m));
    }

    private static Charge Groceries(decimal amount = 45m, Guid? fundedBy = null) =>
        Charge.Create(
            UserId.Create(User), "Groceries", Money.Create(amount, "USD"),
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
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(UserId u, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task PersonalCharge_PostsDrExpenseCrCash_InTheirOwnBook()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);
        var charge = Groceries();
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);

        var entry = Assert.Single(repo.JournalEntries);
        Assert.Equal(0m, entry.Postings.Sum(p => p.SignedAmount));
        Assert.Equal(PersonalChart.ExpenseCode("groceries"),
            repo.CodeOf(entry.Postings.Single(p => p.Direction == EntryDirection.Debit).AccountId));
        Assert.Equal(PersonalChart.CashCode,
            repo.CodeOf(entry.Postings.Single(p => p.Direction == EntryDirection.Credit).AccountId));
    }

    [Fact]
    public async Task PersonalCharge_OnACard_CreditsTheCardSoTheDebtGrows()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var charges = new ChargeStore();
        var manager = new BookkeepingManager(repo, new CashBasisJournalizingEngine(), charges, null!);

        var cardId = await manager.OpenDebtAccountAsync(Card(0m));
        var charge = Groceries(fundedBy: cardId);
        await charges.AddAsync(charge);

        await manager.ConvergePersonalChargeAsync(charge.Id.Value);

        var onCard = repo.JournalEntries
            .SelectMany(e => e.Postings)
            .Where(p => p.AccountId.Value == cardId);

        // Liability credited, so owing 45 more. No special case anywhere — the account type
        // already carries the difference between a card and a bank account.
        Assert.Equal(45m, LedgerMath.AccountBalance(NormalBalance.Credit, onCard));
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

        Assert.Null(await repo.GetLedgerByOwnerAsync(LedgerOwnerType.Group, User));
        Assert.NotNull(await repo.GetLedgerByOwnerAsync(LedgerOwnerType.User, User));
    }
}
