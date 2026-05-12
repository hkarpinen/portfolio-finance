using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedExpenseSplitTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var expenseId = ExpenseId.New();
        var householdId = HouseholdId.New();
        var membershipId = MembershipId.New();
        var userId = UserId.New();
        var amount = Money.Create(50m, "USD");

        // Act
        var split = ExpenseSplit.Create(expenseId, householdId, membershipId, userId, amount);

        // Assert
        Assert.Equal(expenseId, split.ExpenseId);
        Assert.Equal(householdId, split.HouseholdId);
        Assert.Equal(membershipId, split.MembershipId);
        Assert.Equal(userId, split.UserId);
        Assert.Equal(50m, split.Amount.Amount);
    }

    [Fact]
    public void Create_ShouldRaise_ExpenseSplitCreatedEvent()
    {
        // Arrange / Act
        var split = ExpenseSplit.Create(ExpenseId.New(), HouseholdId.New(), MembershipId.New(), UserId.New(), Money.Create(25m, "USD"));

        // Assert
        Assert.Single(split.GetDomainEvents());
        Assert.IsType<ExpenseSplitCreated>(split.GetDomainEvents()[0]);
    }
}
