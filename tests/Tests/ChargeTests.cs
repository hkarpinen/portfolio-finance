using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ChargeTests
{
    private static Charge CreateValidCharge(
        UserId? userId = null,
        decimal amount = 75m,
        ChargeCategory category = ChargeCategory.Utilities,
        string title = "Phone Bill")
    {
        return Charge.CreateOwn(
            userId ?? UserId.New(),
            title,
            Money.Create(amount, "USD"),
            category,
            DateTime.UtcNow.Date.AddDays(3));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var userId = UserId.New();
        var dueDate = DateTime.UtcNow.Date.AddDays(7);
        var amount = Money.Create(120m, "USD");

        var bill = Charge.CreateOwn(userId, "Netflix", amount, ChargeCategory.Other, dueDate, description: "Streaming");

        Assert.Equal(userId, bill.EnteredBy);
        Assert.Equal("Netflix", bill.Title);
        Assert.Equal(120m, bill.Amount.Amount);
        Assert.Equal(ChargeCategory.Other, bill.Category);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.Equal("Streaming", bill.Description);
        Assert.True(bill.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_ChargeCreatedEvent()
    {
        var bill = CreateValidCharge();

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Charge.CreateOwn(UserId.New(), "  ", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Money is signed now (refunds, contra entries, inflows). The non-negative
        // invariant for an expense lives on the Charge aggregate, not on Money.
        Assert.Throws<ArgumentException>(() =>
            Charge.CreateOwn(UserId.New(), "Rent", Money.Create(-10m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Update_ShouldChangeTitleAmountCategoryAndDueDate()
    {
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        bill.Update("Updated Bill", Money.Create(200m, "USD"), ChargeCategory.Rent, newDueDate, description: "New desc");

        Assert.Equal("Updated Bill", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ChargeCategory.Rent, bill.Category);
        Assert.Equal(newDueDate, bill.DueDate);
        Assert.Equal("New desc", bill.Description);
    }

    [Fact]
    public void Update_ShouldRaise_ChargeUpdatedEvent()
    {
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();

        bill.Update("New Title", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeUpdated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        var bill = CreateValidCharge();

        Assert.Throws<ArgumentException>(() =>
            bill.Update("", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var bill = CreateValidCharge();

        bill.Deactivate();

        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldRaise_ChargeDeactivatedEvent()
    {
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();

        bill.Deactivate();

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var bill = CreateValidCharge();
        bill.Deactivate();

        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        var bill = CreateValidCharge();

        var result = bill.TryDeactivate();

        Assert.True(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        var bill = CreateValidCharge();
        bill.Deactivate();

        var result = bill.TryDeactivate();

        Assert.False(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEvents()
    {
        var bill = CreateValidCharge();
        Assert.NotEmpty(bill.GetDomainEvents());

        bill.ClearDomainEvents();

        Assert.Empty(bill.GetDomainEvents());
    }
}
