using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

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
            PostingLine.Debit(Cash, Usd(700)),
            PostingLine.Credit(MemberHank, Usd(700)),
        });

        Assert.Equal(2, entry.Postings.Count);
        Assert.True(LedgerMath.IsBalanced(entry.Postings));
        Assert.IsType<JournalEntryPosted>(entry.GetDomainEvents()[0]);
    }

    [Fact]
    public void Post_UnbalancedEntry_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "lopsided", new[]
            {
                PostingLine.Debit(Cash, Usd(700)),
                PostingLine.Credit(MemberHank, Usd(300)),   // 700 ≠ 300
            }));
        Assert.Contains("does not balance", ex.Message);
    }

    [Fact]
    public void Post_SingleLine_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "one-sided", new[] { PostingLine.Debit(Cash, Usd(700)) }));
    }

    [Fact]
    public void Post_NegativeAmount_Throws()
    {
        // Money is signed, but a posting amount must be positive — the direction carries the sign.
        Assert.Throws<ArgumentException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "neg", new[]
            {
                PostingLine.Debit(Cash, Usd(-700)),
                PostingLine.Credit(MemberHank, Usd(-700)),
            }));
    }

    [Fact]
    public void Post_MixedCurrency_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JournalEntry.Post(L, DateTime.UtcNow, "fx", new[]
            {
                PostingLine.Debit(Cash, Money.Create(700, "USD")),
                PostingLine.Credit(MemberHank, Money.Create(700, "EUR")),
            }));
    }

    [Fact]
    public void AccountBalance_DebitNormal_Asset()
    {
        // Cash: +700 +300 −1000 = 0 (asset, debit-normal)
        var postings = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "in", new[]  { PostingLine.Debit(Cash, Usd(700)),  PostingLine.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "in", new[]  { PostingLine.Debit(Cash, Usd(300)),  PostingLine.Credit(MemberBob, Usd(300)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "out", new[] { PostingLine.Debit(MemberHank, Usd(700)), PostingLine.Debit(MemberBob, Usd(300)), PostingLine.Credit(Cash, Usd(1000)) }),
        }.SelectMany(e => e.Postings).Where(p => p.AccountId == Cash);

        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, postings));
    }

    [Fact]
    public void AccountBalance_CreditNormal_Equity_PositiveWhenNetCredited()
    {
        // Hank (equity, credit-normal): +700 contributed, −700 consumed as his share → exactly 0 after
        // one full cycle.
        var entries = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "in",  new[] { PostingLine.Debit(Cash, Usd(700)), PostingLine.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "out", new[] { PostingLine.Debit(MemberHank, Usd(700)), PostingLine.Credit(Cash, Usd(700)) }),
        };
        var hank = entries.SelectMany(e => e.Postings).Where(p => p.AccountId == MemberHank);
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, hank));

        // A member who only contributed shows a positive (credit) balance — the group owes them.
        var contributedOnly = entries[0].Postings.Where(p => p.AccountId == MemberHank);
        Assert.Equal(700m, LedgerMath.AccountBalance(NormalBalance.Credit, contributedOnly));
    }

    [Fact]
    public void PooledScenario_TrialBalances_AndConserves()
    {
        // Rent $1,000, Hank $700 / Bob $300; both contribute, pool pays.
        var entries = new[]
        {
            JournalEntry.Post(L, DateTime.UtcNow, "Hank contributes", new[] { PostingLine.Debit(Cash, Usd(700)), PostingLine.Credit(MemberHank, Usd(700)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "Bob contributes",  new[] { PostingLine.Debit(Cash, Usd(300)), PostingLine.Credit(MemberBob, Usd(300)) }),
            JournalEntry.Post(L, DateTime.UtcNow, "Pool pays vendor", new[]
            {
                PostingLine.Debit(MemberHank, Usd(700)),
                PostingLine.Debit(MemberBob, Usd(300)),
                PostingLine.Credit(Cash, Usd(1000)),
            }),
        };
        var all = entries.SelectMany(e => e.Postings).ToList();

        var (debits, credits) = LedgerMath.TrialBalance(all);
        Assert.Equal(2000m, debits);
        Assert.Equal(2000m, credits);
        Assert.True(LedgerMath.IsBalanced(all));

        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Debit, all.Where(p => p.AccountId == Cash)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == MemberHank)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, all.Where(p => p.AccountId == MemberBob)));
    }

    [Fact]
    public void Reverse_MirrorsPostings_AndNetsToZero()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "Bob reimburses Hank", new[]
        {
            PostingLine.Debit(MemberBob, Usd(300)),
            PostingLine.Credit(MemberHank, Usd(300)),
        });

        var reversal = original.Reverse(DateTime.UtcNow);

        Assert.Equal(original.Id, reversal.ReversalOfEntryId);
        Assert.True(LedgerMath.IsBalanced(reversal.Postings));
        Assert.IsType<JournalEntryReversed>(reversal.GetDomainEvents()[0]);

        var revBob = reversal.Postings.Single(p => p.AccountId == MemberBob);
        Assert.Equal(EntryDirection.Credit, revBob.Direction);

        var both = original.Postings.Concat(reversal.Postings).ToList();
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, both.Where(p => p.AccountId == MemberHank)));
        Assert.Equal(0m, LedgerMath.AccountBalance(NormalBalance.Credit, both.Where(p => p.AccountId == MemberBob)));
    }

    [Fact]
    public void Reverse_AReversal_Throws()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "x", new[]
        {
            PostingLine.Debit(MemberBob, Usd(300)),
            PostingLine.Credit(MemberHank, Usd(300)),
        });
        var reversal = original.Reverse(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => reversal.Reverse(DateTime.UtcNow));
    }

    [Fact]
    public void Reverse_MarksOriginal_ReversedBy()
    {
        var original = JournalEntry.Post(L, DateTime.UtcNow, "x", new[]
        {
            PostingLine.Debit(MemberBob, Usd(300)),
            PostingLine.Credit(MemberHank, Usd(300)),
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
            PostingLine.Debit(MemberBob, Usd(300)),
            PostingLine.Credit(MemberHank, Usd(300)),
        });
        original.Reverse(DateTime.UtcNow);

        // The original now carries ReversedByEntryId — reversing it again must be rejected so the partial
        // unique index can never see two active postings under one source.
        Assert.Throws<InvalidOperationException>(() => original.Reverse(DateTime.UtcNow));
    }
}
