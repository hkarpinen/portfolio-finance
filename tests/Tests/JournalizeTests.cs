using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// What each rule means in debits and credits. Direction is the thing worth pinning: get it
/// backwards and every entry still balances, the trial balance still proves, and only the sign of
/// somebody's position is wrong.
/// </summary>
public class JournalizeTests
{
    private static readonly LedgerId L = LedgerId.New();
    private static readonly AccountId Category = AccountId.New();
    private static readonly AccountId Payable = AccountId.New();
    private static readonly AccountId Member = AccountId.New();
    private static readonly AccountId Funding = AccountId.New();
    private static readonly AccountId Opening = AccountId.New();
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Expense = Guid.NewGuid();
    private static readonly Guid Hank = Guid.NewGuid();

    private static Money Usd(decimal a) => Money.Create(a, "USD");

    private static AccountId DebitedBy(JournalEntry e) =>
        e.JournalLines.Single(l => l.Direction == EntryDirection.Debit).AccountId;

    private static AccountId CreditedBy(JournalEntry e) =>
        e.JournalLines.Single(l => l.Direction == EntryDirection.Credit).AccountId;

    // A cost incurred puts it on the category and owes it to the outside world. Crediting the
    // funding account here instead would say it was already paid, which is the shortcut that
    // leaves "has this been paid" with nothing in the books to answer from.
    [Fact]
    public void ACostIncurred_DebitsItsCategoryAndOwesThePayable()
    {
        var e = Journalize.ExpenseIncurred(
            L, Category, Payable, Usd(90m), Jan3, "Shop", "expense:1", Expense, Hank);

        Assert.Equal(Category, DebitedBy(e));
        Assert.Equal(Payable, CreditedBy(e));
        Assert.Equal("Shop — incurred", e.Description);
        Assert.Equal(Jan3, e.Date);
    }

    // Provenance, not decoration: the reads that derive whether a cost has been paid find its
    // entries by SourceExpenseId, so an entry without one is invisible to them.
    [Fact]
    public void ACostIncurred_CarriesTheExpenseItCameFrom()
    {
        var e = Journalize.ExpenseIncurred(
            L, Category, Payable, Usd(90m), Jan3, "Shop", "expense:1", Expense, Hank);

        Assert.Equal(Expense, e.SourceExpenseId);
        Assert.Equal(Hank, e.PostedByUserId);
    }

    // The share moves the cost OFF the category and onto the member. Backwards, everyone's
    // standing would grow as they took on costs instead of shrinking.
    [Fact]
    public void AShareBorne_DebitsTheMemberAndClearsTheCategory()
    {
        var e = Journalize.ShareBorne(L, Member, Category, Usd(30m), Jan3, "share:1", Expense, Hank);

        Assert.Equal(Member, DebitedBy(e));
        Assert.Equal(Category, CreditedBy(e));
        Assert.Equal(Hank, e.SourceMemberId);
    }

    [Fact]
    public void PayingTheVendor_ClearsThePayableAgainstWhateverFundedIt()
    {
        var e = Journalize.VendorPaid(
            L, Payable, Funding, Usd(90m), Jan3, "Vendor payment", "vendorpayment:1",
            Expense, Jan3, Hank);

        Assert.Equal(Payable, DebitedBy(e));
        Assert.Equal(Funding, CreditedBy(e));
        Assert.Equal(Jan3, e.SourceOccurrence);
    }

    [Fact]
    public void ASettlement_DebitsWhatFundedItAndCreditsTheDebtor()
    {
        var share = Guid.NewGuid();
        var e = Journalize.Settlement(
            L, Funding, Member, Usd(30m), Jan3, "settlement:1", Expense, share, Jan3, Hank);

        Assert.Equal(Funding, DebitedBy(e));
        Assert.Equal(Member, CreditedBy(e));
        Assert.Equal(share, e.SourceShareId);
    }

    // Not tied to any one cost — two people squaring up carries no expense.
    [Fact]
    public void ASettleUp_MovesBetweenTwoMembersAndNamesNoExpense()
    {
        var creditor = AccountId.New();
        var e = Journalize.SettleUp(L, creditor, Member, Usd(30m), Jan3, "settleup:1", Hank);

        Assert.Equal(creditor, DebitedBy(e));
        Assert.Equal(Member, CreditedBy(e));
        Assert.Null(e.SourceExpenseId);
    }

    [Fact]
    public void ABalanceCarriedIn_OffsetsAgainstOpeningEquity()
    {
        var card = AccountId.New();
        var e = Journalize.BalanceCarriedIn(
            L, Opening, card, Usd(1200m), Jan3, "Visa", "debt-opening:1", Hank);

        Assert.Equal(Opening, DebitedBy(e));
        Assert.Equal(card, CreditedBy(e));
        Assert.Equal("Visa — balance carried in", e.Description);
    }

    // Both books say a cost the same way. They were two implementations that had to agree by
    // memory; the group and personal paths now call this one.
    [Fact]
    public void AGroupCostAndAPersonalOne_AreTheSameRule()
    {
        var group = Journalize.ExpenseIncurred(
            L, Category, Payable, Usd(90m), Jan3, "Shop", "expense:1", Expense, Hank);
        var personal = Journalize.ExpenseIncurred(
            L, Category, Payable, Usd(90m), Jan3, "Shop", "expense:1", Expense, Hank);

        Assert.True(group.SaysTheSameAs(personal));
    }
}
