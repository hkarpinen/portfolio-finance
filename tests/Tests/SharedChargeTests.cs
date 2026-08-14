using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedChargeTests
{
    private static (GroupId, UserId) NewIds() => (GroupId.Create(Guid.NewGuid()), UserId.New());

    private static Charge CreateValidCharge(GroupId? groupId = null, UserId? createdBy = null)
    {
        var hId = groupId ?? GroupId.Create(Guid.NewGuid());
        var uId = createdBy ?? UserId.New();
        return Charge.Create(AccountingEntity.Household(hId),
            uId,
            "Test Bill",
            Money.Create(100m, "USD"),
            ChargeCategory.Utilities,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        var bill = Charge.Create(AccountingEntity.Household(hId), uId, "Electricity", Money.Create(80m, "USD"), ChargeCategory.Utilities, dueDate);

        Assert.Equal("Electricity", bill.Title);
        Assert.Equal(hId, bill.GroupId);
        Assert.Equal(uId, bill.EnteredBy);
        Assert.Equal(dueDate, bill.DueDate);
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
        var (hId, uId) = NewIds();

        Assert.Throws<ArgumentException>(() =>
            Charge.Create(AccountingEntity.Household(hId), uId, "", Money.Create(100m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var bill = CreateValidCharge();

        bill.Deactivate();

        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        var bill = CreateValidCharge();
        bill.Deactivate();

        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
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
    public void Update_ShouldChangeTitleAndAmount()
    {
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        bill.Update("Updated Title", Money.Create(200m, "USD"), ChargeCategory.Rent, newDueDate);

        Assert.Equal("Updated Title", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ChargeCategory.Rent, bill.Category);
    }

    [Fact]
    public void CreateGroup_WithPayer_ShouldStoreAndEmitPayer()
    {
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();

        var bill = Charge.Create(AccountingEntity.Household(hId), uId, "Rent", Money.Create(1900m, "USD"), ChargeCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);

        // Assert — stored on the aggregate
        Assert.Equal(payer, bill.PayerUserId);

        // Assert — carried on the ChargeCreated event (so read-sides/consumers can see it)
        var created = Assert.IsType<ChargeCreated>(bill.GetDomainEvents()[0]);
        Assert.Equal(payer, created.PayerUserId);
    }

    [Fact]
    public void Update_ShouldCarryEffectivePayer_OnChargeUpdatedEvent()
    {
        // Arrange — created with an initial payer
        var (hId, uId) = NewIds();
        var initialPayer = Guid.NewGuid();
        var bill = Charge.Create(AccountingEntity.Household(hId), uId, "Rent", Money.Create(1900m, "USD"), ChargeCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: initialPayer);
        bill.ClearDomainEvents();

        // Act — change the payer via Update
        var newPayer = Guid.NewGuid();
        bill.Update(
            "Rent", Money.Create(1900m, "USD"), ChargeCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: newPayer);

        // Assert — the event reflects the effective payer after the update
        var updated = Assert.IsType<ChargeUpdated>(bill.GetDomainEvents()[0]);
        Assert.Equal(newPayer, updated.PayerUserId);
        Assert.Equal(newPayer, bill.PayerUserId);
    }

    [Fact]
    public void Update_WithNullPayer_ShouldLeaveExistingPayerUnchanged()
    {
        // Arrange — PATCH semantics: a null payer in Update means "leave as-is"
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();
        var bill = Charge.Create(AccountingEntity.Household(hId), uId, "Rent", Money.Create(1900m, "USD"), ChargeCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);
        bill.ClearDomainEvents();

        // Act — update other fields, leave payer null
        bill.Update("Rent v2", Money.Create(2000m, "USD"), ChargeCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1));

        // Assert — payer preserved, and the event still carries it
        Assert.Equal(payer, bill.PayerUserId);
        var updated = Assert.IsType<ChargeUpdated>(bill.GetDomainEvents()[0]);
        Assert.Equal(payer, updated.PayerUserId);
    }
}
