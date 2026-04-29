using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class BillSplitTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var billId = BillId.New();
        var householdId = HouseholdId.New();
        var membershipId = MembershipId.New();
        var userId = UserId.New();
        var amount = Money.Create(50m, "USD");

        // Act
        var split = BillSplit.Create(billId, householdId, membershipId, userId, amount);

        // Assert
        Assert.Equal(billId, split.BillId);
        Assert.Equal(householdId, split.HouseholdId);
        Assert.Equal(membershipId, split.MembershipId);
        Assert.Equal(userId, split.UserId);
        Assert.Equal(50m, split.Amount.Amount);
        Assert.False(split.IsClaimed);
    }

    [Fact]
    public void Create_ShouldRaise_BillSplitCreatedEvent()
    {
        // Arrange / Act
        var split = BillSplit.Create(BillId.New(), HouseholdId.New(), MembershipId.New(), UserId.New(), Money.Create(25m, "USD"));

        // Assert
        Assert.Single(split.GetDomainEvents());
        Assert.IsType<BillSplitCreated>(split.GetDomainEvents()[0]);
    }
}
