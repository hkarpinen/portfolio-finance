using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedAllocationTests
{
    private static Charge GroupCharge(decimal amount = 100m) => Charge.Create(
            AccountingEntity.Household(GroupId.Create(Guid.NewGuid())), UserId.New(), "Rent", Money.Create(amount, "USD"),
        ChargeCategory.Rent, DateTime.UtcNow.Date);

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var charge = GroupCharge();
        var userId = UserId.New();
        var amount = Money.Create(50m, "USD");

        var split = Allocation.Create(charge, userId, amount);

        Assert.Equal(charge.Id, split.ChargeId);
        Assert.Equal(userId, split.UserId);
        Assert.Equal(50m, split.Amount.Amount);
    }

    [Fact]
    public void Create_ShouldRaise_AllocationCreatedEvent()
    {
        var split = Allocation.Create(GroupCharge(), UserId.New(), Money.Create(25m, "USD"));

        Assert.Single(split.GetDomainEvents());
        Assert.IsType<AllocationCreated>(split.GetDomainEvents()[0]);
    }

    // The group is the charge's. It is named on the event only because a reversal can outlive the
    // allocation, and it must be the group of the charge the share is actually on.
    [Fact]
    public void TheGroupOnTheEvent_IsTheChargesGroup()
    {
        var charge = GroupCharge();
        var split = Allocation.Create(charge, UserId.New(), Money.Create(25m, "USD"));
        split.ClearDomainEvents();

        split.Update(charge, Money.Create(30m, "USD"));

        var updated = Assert.IsType<AllocationUpdated>(split.GetDomainEvents()[0]);
        Assert.Equal(charge.GroupId, updated.GroupId);
    }

    [Fact]
    public void ASplit_RefusesACharge_ItIsNotOn()
    {
        var split = Allocation.Create(GroupCharge(), UserId.New(), Money.Create(25m, "USD"));
        var somebodyElses = GroupCharge();

        Assert.Throws<InvalidOperationException>(
            () => split.Update(somebodyElses, Money.Create(30m, "USD")));
        Assert.Throws<InvalidOperationException>(() => split.Remove(somebodyElses));
    }

    [Fact]
    public void APersonalCharge_HasNoSharesToAllocate()
    {
        var personal = Charge.CreateOwn(UserId.New(), "Gym", Money.Create(40m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date);

        Assert.Throws<InvalidOperationException>(
            () => Allocation.Create(personal, UserId.New(), Money.Create(40m, "USD")));
    }
}
