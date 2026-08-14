using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;
using Finance.Domain.Engines;
using Infrastructure.Queries;

namespace Tests;

public class LedgerTests
{
    private static readonly LedgerId L = LedgerId.New();
    private static readonly AccountId Cash = AccountId.New();
    private static readonly AccountId MemberHank = AccountId.New();
    private static readonly AccountId MemberBob = AccountId.New();
    private static Money Usd(decimal a) => Money.Create(a, "USD");

    [Theory]
    [InlineData(AccountType.Asset, NormalBalance.Debit)]
    [InlineData(AccountType.Expense, NormalBalance.Debit)]
    [InlineData(AccountType.Liability, NormalBalance.Credit)]
    [InlineData(AccountType.Equity, NormalBalance.Credit)]
    [InlineData(AccountType.Income, NormalBalance.Credit)]
    public void NormalBalance_FollowsAccountType(AccountType type, NormalBalance expected)
    {
        Assert.Equal(expected, type.NormalBalance());
        Assert.Equal(expected, Account.Open(L, "1000", "x", type).NormalBalance);
    }

    [Fact]
    public void Post_BalancedEntry_Succeeds()
    {
        var entry = JournalEntry.Post(L, DateTime.UtcNow, "Hank contributes $700", new[]
        {
            JournalLineDraft.Debit(Cash, Usd(700)),
            JournalLineDraft.Credit(MemberHank, Usd(700)),
        });

        Assert.Equal(2, entry.JournalLines.Count);
        Assert.IsType<JournalEntryPosted>(entry.GetDomainEvents()[0]);
    }

    [Fact]
    public void Post_UnbalancedEntry_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "lopsided", new[]
            {
                JournalLineDraft.Debit(Cash, Usd(700)),
                JournalLineDraft.Credit(MemberHank, Usd(300)),   // 700 ≠ 300
            }));
        Assert.Contains("does not balance", ex.Message);
    }

    [Fact]
    public void Post_SingleLine_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "one-sided", new[] { JournalLineDraft.Debit(Cash, Usd(700)) }));
    }

    [Fact]
    public void Post_NegativeAmount_Throws()
    {
        // Money is signed, but a journalLine amount must be positive — the direction carries the sign.
        Assert.Throws<ArgumentException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "neg", new[]
            {
                JournalLineDraft.Debit(Cash, Usd(-700)),
                JournalLineDraft.Credit(MemberHank, Usd(-700)),
            }));
    }

    [Fact]
    public void Post_MixedCurrency_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "fx", new[]
            {
                JournalLineDraft.Debit(Cash, Money.Create(700, "USD")),
                JournalLineDraft.Credit(MemberHank, Money.Create(700, "EUR")),
            }));
    }

    [Fact]
    public void AccountBalance_DebitNormal_Asset()
    {
        // Cash: +700 +300 −1000 = 0 (asset, debit-normal)
        var lines = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "in", new[]  { JournalLineDraft.Debit(Cash, Usd(700)),  JournalLineDraft.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "in", new[]  { JournalLineDraft.Debit(Cash, Usd(300)),  JournalLineDraft.Credit(MemberBob, Usd(300)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "out", new[] { JournalLineDraft.Debit(MemberHank, Usd(700)), JournalLineDraft.Debit(MemberBob, Usd(300)), JournalLineDraft.Credit(Cash, Usd(1000)) }),
        }.SelectMany(e => e.JournalLines).Where(p => p.AccountId == Cash);

        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Debit, lines));
    }

    [Fact]
    public void AccountBalance_CreditNormal_Equity_PositiveWhenNetCredited()
    {
        // Hank (equity, credit-normal): +700 contributed, −700 consumed as his share → exactly 0 after
        // one full cycle.
        var entries = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "in",  new[] { JournalLineDraft.Debit(Cash, Usd(700)), JournalLineDraft.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "out", new[] { JournalLineDraft.Debit(MemberHank, Usd(700)), JournalLineDraft.Credit(Cash, Usd(700)) }),
        };
        var hank = entries.SelectMany(e => e.JournalLines).Where(p => p.AccountId == MemberHank);
        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, hank));

        // A member who only contributed shows a positive (credit) balance — the group owes them.
        var contributedOnly = entries[0].JournalLines.Where(p => p.AccountId == MemberHank);
        Assert.Equal(700m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, contributedOnly));
    }

    [Fact]
    public void PooledScenario_TrialBalances_AndConserves()
    {
        // Rent $1,000, Hank $700 / Bob $300; both contribute, pool pays.
        var entries = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "Hank contributes", new[] { JournalLineDraft.Debit(Cash, Usd(700)), JournalLineDraft.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "Bob contributes",  new[] { JournalLineDraft.Debit(Cash, Usd(300)), JournalLineDraft.Credit(MemberBob, Usd(300)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "Pool pays vendor", new[]
            {
                JournalLineDraft.Debit(MemberHank, Usd(700)),
                JournalLineDraft.Debit(MemberBob, Usd(300)),
                JournalLineDraft.Credit(Cash, Usd(1000)),
            }),
        };
        var all = entries.SelectMany(e => e.JournalLines).ToList();

        var (debits, credits) = LedgerBalanceReads.TrialBalance(all);
        Assert.Equal(2000m, debits);
        Assert.Equal(2000m, credits);

        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Debit, all.Where(p => p.AccountId == Cash)));
        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == MemberHank)));
        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == MemberBob)));
    }

    [Fact]
    public void Reverse_MirrorsJournalLines_AndNetsToZero()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "Bob reimburses Hank", new[]
        {
            JournalLineDraft.Debit(MemberBob, Usd(300)),
            JournalLineDraft.Credit(MemberHank, Usd(300)),
        });

        var reversal = original.Reverse(DateTime.UtcNow);

        Assert.Equal(original.Id, reversal.ReversalOfEntryId);
        Assert.IsType<JournalEntryReversed>(reversal.GetDomainEvents()[0]);

        var revBob = reversal.JournalLines.Single(p => p.AccountId == MemberBob);
        Assert.Equal(EntryDirection.Credit, revBob.Direction);

        var both = original.JournalLines.Concat(reversal.JournalLines).ToList();
        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, both.Where(p => p.AccountId == MemberHank)));
        Assert.Equal(0m, LedgerBalanceReads.AccountBalance(NormalBalance.Credit, both.Where(p => p.AccountId == MemberBob)));
    }

    [Fact]
    public void Reverse_AReversal_Throws()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "x", new[]
        {
            JournalLineDraft.Debit(MemberBob, Usd(300)),
            JournalLineDraft.Credit(MemberHank, Usd(300)),
        });
        var reversal = original.Reverse(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => reversal.Reverse(DateTime.UtcNow));
    }

    [Fact]
    public void Reverse_MarksOriginal_ReversedBy()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "x", new[]
        {
            JournalLineDraft.Debit(MemberBob, Usd(300)),
            JournalLineDraft.Credit(MemberHank, Usd(300)),
        });

        Assert.Null(original.ReversedByEntryId);

        var reversal = original.Reverse(DateTime.UtcNow);

        Assert.Equal(reversal.Id.Value, original.ReversedByEntryId);
    }

    [Fact]
    public void Reverse_AnAlreadyReversedEntry_Throws()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "x", new[]
        {
            JournalLineDraft.Debit(MemberBob, Usd(300)),
            JournalLineDraft.Credit(MemberHank, Usd(300)),
        });
        original.Reverse(DateTime.UtcNow);

        // The original now carries ReversedByEntryId — reversing it again must be rejected so the partial
        // unique index can never see two active journal_lines under one source.
        Assert.Throws<InvalidOperationException>(() => original.Reverse(DateTime.UtcNow));
    }

    [Fact]
    public void Post_RecordsWhoseActionCausedTheEntry()
    {
        var actor = Guid.NewGuid();
        var ledger = LedgerId.New();
        var lines = new[]
        {
            JournalLineDraft.Debit(AccountId.New(), Money.Create(10m, "USD")),
            JournalLineDraft.Credit(AccountId.New(), Money.Create(10m, "USD")),
        };

        var entry = JournalEntry.Post(
            ledger, DateTime.UtcNow, "Something", lines, postedByUserId: actor);

        Assert.Equal(actor, entry.PostedByUserId);
    }

    [Fact]
    public void Reverse_AttributesTheReversalToWhoeverCausedIt_NotTheOriginalAuthor()
    {
        var author = Guid.NewGuid();
        var corrector = Guid.NewGuid();
        var ledger = LedgerId.New();
        var lines = new[]
        {
            JournalLineDraft.Debit(AccountId.New(), Money.Create(10m, "USD")),
            JournalLineDraft.Credit(AccountId.New(), Money.Create(10m, "USD")),
        };
        var original = JournalEntry.Post(ledger, DateTime.UtcNow, "Something", lines, postedByUserId: author);

        var reversal = original.Reverse(original.Date, reversedByUserId: corrector);

        // Undoing an entry is a new act. Copying the author would credit the correction to
        // somebody who was not there.
        Assert.Equal(corrector, reversal.PostedByUserId);
        Assert.Equal(author, original.PostedByUserId);
    }

    // Where the rule actually lives. An unbalanced entry cannot be built, so nothing downstream has
    // to check for one — which is why asserting balance on an entry that already exists proved
    // nothing the constructor had not already guaranteed.
    [Fact]
    public void AnEntryThatDoesNotBalance_CannotBeBuilt()
    {
        var error = Assert.Throws<InvalidOperationException>(() => JournalEntry.Post(
            L, DateTime.UtcNow, "Lopsided",
            [JournalLineDraft.Debit(Cash, Usd(700)), JournalLineDraft.Credit(MemberHank, Usd(500))]));

        Assert.Contains("balance", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
