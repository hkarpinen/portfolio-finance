using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedAllocationTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var expenseId = ChargeId.New();
        var groupId = GroupId.Create(Guid.NewGuid());
        var userId = UserId.New();
        var amount = Money.Create(50m, "USD");

        var split = Allocation.Create(expenseId, groupId, userId, amount);

        Assert.Equal(expenseId, split.ChargeId);
        Assert.Equal(groupId, split.GroupId);
        Assert.Equal(userId, split.UserId);
        Assert.Equal(50m, split.Amount.Amount);
    }

    [Fact]
    public void Create_ShouldRaise_AllocationCreatedEvent()
    {
        var split = Allocation.Create(ChargeId.New(), GroupId.Create(Guid.NewGuid()), UserId.New(), Money.Create(25m, "USD"));

        Assert.Single(split.GetDomainEvents());
        Assert.IsType<AllocationCreated>(split.GetDomainEvents()[0]);
    }
}
