using Bills.Domain.Aggregates;
using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Tests;

public class MoneyTests
{
    [Fact]
    public void Create_ShouldSetAmountAndCurrency()
    {
        // Arrange / Act
        var money = Money.Create(100.00m, "USD");

        // Assert
        Assert.Equal(100.00m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUppercase()
    {
        // Arrange / Act
        var money = Money.Create(50m, "usd");

        // Assert
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(-1m, "USD"));
    }

    [Fact]
    public void Create_EmptyCurrency_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(10m, ""));
    }

    [Fact]
    public void Create_InvalidCurrencyLength_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(10m, "US"));
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        // Arrange
        var a = Money.Create(100m, "USD");
        var b = Money.Create(50m, "USD");

        // Act
        var result = a.Add(b);

        // Assert
        Assert.Equal(150m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Add_DifferentCurrencies_ShouldThrow()
    {
        // Arrange
        var usd = Money.Create(100m, "USD");
        var eur = Money.Create(100m, "EUR");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void Subtract_SameCurrency_ShouldReturnDifference()
    {
        // Arrange
        var a = Money.Create(100m, "USD");
        var b = Money.Create(30m, "USD");

        // Act
        var result = a.Subtract(b);

        // Assert
        Assert.Equal(70m, result.Amount);
    }

    [Fact]
    public void Subtract_DifferentCurrencies_ShouldThrow()
    {
        // Arrange
        var usd = Money.Create(100m, "USD");
        var eur = Money.Create(50m, "EUR");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => usd.Subtract(eur));
    }

    [Fact]
    public void Multiply_ByFactor_ShouldReturnScaledAmount()
    {
        // Arrange
        var money = Money.Create(100m, "USD");

        // Act
        var result = money.Multiply(1.5m);

        // Assert
        Assert.Equal(150m, result.Amount);
    }

    [Fact]
    public void Multiply_ByNegativeFactor_ShouldThrow()
    {
        // Arrange
        var money = Money.Create(100m, "USD");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => money.Multiply(-1m));
    }
}

public class RecurrenceScheduleTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);

        // Act
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);

        // Assert
        Assert.Equal(RecurrenceFrequency.Monthly, schedule.Frequency);
        Assert.Equal(start, schedule.StartDate);
        Assert.Null(schedule.EndDate);
    }

    [Fact]
    public void Create_EndDateBeforeStartDate_ShouldThrow()
    {
        // Arrange
        var start = new DateTime(2024, 6, 1);
        var end = new DateTime(2024, 1, 1);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start, end));
    }

    [Fact]
    public void GetOccurrencesInRange_Monthly_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 7, 1);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(6, occurrences.Count); // Jan, Feb, Mar, Apr, May, Jun
    }

    [Fact]
    public void GetOccurrencesInRange_Weekly_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1); // Monday
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 1, 29); // 4 weeks

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(4, occurrences.Count);
    }

    [Fact]
    public void GetOccurrencesInRange_Annually_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2020, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Annually, start);
        var rangeStart = new DateTime(2020, 1, 1);
        var rangeEnd = new DateTime(2024, 1, 1);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(4, occurrences.Count); // 2020, 2021, 2022, 2023
    }

    [Fact]
    public void GetOccurrencesInRange_StartAfterRange_ShouldReturnEmpty()
    {
        // Arrange
        var start = new DateTime(2025, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 12, 31);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Empty(occurrences);
    }

    [Fact]
    public void GetAmountForPeriod_Monthly_ShouldReturnTotalAmount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var amount = Money.Create(100m, "USD");
        var periodStart = new DateTime(2024, 1, 1);
        var periodEnd = new DateTime(2024, 4, 1);

        // Act
        var total = schedule.GetAmountForPeriod(amount, periodStart, periodEnd);

        // Assert
        Assert.Equal(300m, total); // Jan, Feb, Mar = 3 * 100
    }
}

public class HouseholdTests
{
    private static UserId NewUserId() => UserId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var ownerId = NewUserId();

        // Act
        var household = Household.Create("My Household", ownerId, "USD", "A description");

        // Assert
        Assert.Equal("My Household", household.Name);
        Assert.Equal(ownerId, household.OwnerId);
        Assert.Equal("USD", household.CurrencyCode);
        Assert.Equal("A description", household.Description);
        Assert.True(household.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_HouseholdCreatedEvent()
    {
        // Arrange / Act
        var household = Household.Create("Test", NewUserId());

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdCreated>(household.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldRenamHousehold()
    {
        // Arrange
        var household = Household.Create("Old Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Update("New Name", "New description");

        // Assert
        Assert.Equal("New Name", household.Name);
        Assert.Equal("New description", household.Description);
    }

    [Fact]
    public void Update_EmptyName_ShouldThrow()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());

        // Act / Assert
        Assert.Throws<ArgumentException>(() => household.Update(""));
    }

    [Fact]
    public void Update_ShouldRaise_HouseholdUpdatedEvent()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Update("New Name");

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdUpdated>(household.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());

        // Act
        household.Deactivate();

        // Assert
        Assert.False(household.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => household.Deactivate());
    }

    [Fact]
    public void Deactivate_ShouldRaise_HouseholdDeletedEvent()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Deactivate();

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdDeleted>(household.GetDomainEvents()[0]);
    }
}

public class BillTests
{
    private static (HouseholdId, UserId) NewIds() => (HouseholdId.New(), UserId.New());

    private static Bill CreateValidBill(HouseholdId? householdId = null, UserId? createdBy = null)
    {
        var hId = householdId ?? HouseholdId.New();
        var uId = createdBy ?? UserId.New();
        return Bill.Create(
            hId,
            "Test Bill",
            Money.Create(100m, "USD"),
            BillCategory.Utilities,
            uId,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        // Act
        var bill = Bill.Create(hId, "Electricity", Money.Create(80m, "USD"), BillCategory.Utilities, uId, dueDate);

        // Assert
        Assert.Equal("Electricity", bill.Title);
        Assert.Equal(hId, bill.HouseholdId);
        Assert.Equal(uId, bill.CreatedBy);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.True(bill.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_BillCreatedEvent()
    {
        // Arrange / Act
        var bill = CreateValidBill();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<BillCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var (hId, uId) = NewIds();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Bill.Create(hId, "", Money.Create(100m, "USD"), BillCategory.Other, uId, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_PastDueDate_ShouldThrow()
    {
        // Arrange
        var (hId, uId) = NewIds();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Bill.Create(hId, "Bill", Money.Create(100m, "USD"), BillCategory.Other, uId, DateTime.UtcNow.Date.AddDays(-1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var bill = CreateValidBill();

        // Act
        bill.Deactivate();

        // Assert
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void Deactivate_ShouldRaise_BillDeactivatedEvent()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.ClearDomainEvents();

        // Act
        bill.Deactivate();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<BillDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldChangeTitleAndAmount()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        // Act
        bill.Update("Updated Title", Money.Create(200m, "USD"), BillCategory.Rent, newDueDate);

        // Assert
        Assert.Equal("Updated Title", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(BillCategory.Rent, bill.Category);
    }
}

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

public class HouseholdMembershipTests
{
    private static HouseholdMembership CreateActiveMembership(HouseholdRole role = HouseholdRole.Member)
        => HouseholdMembership.Create(HouseholdId.New(), UserId.New(), role);

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var householdId = HouseholdId.New();
        var userId = UserId.New();

        // Act
        var membership = HouseholdMembership.Create(householdId, userId, HouseholdRole.Owner);

        // Assert
        Assert.Equal(householdId, membership.HouseholdId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(HouseholdRole.Owner, membership.Role);
        Assert.True(membership.IsActive);
        Assert.Null(membership.InvitationCode);
    }

    [Fact]
    public void Create_ShouldRaise_HouseholdMemberJoinedEvent()
    {
        var membership = CreateActiveMembership();
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberJoined>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void CreateWithInvitation_ShouldBeInactive_AndHaveInvitationCode()
    {
        // Arrange
        var householdId = HouseholdId.New();
        var invitedBy = UserId.New();

        // Act
        var membership = HouseholdMembership.CreateWithInvitation(householdId, invitedBy, "INV-123");

        // Assert
        Assert.False(membership.IsActive);
        Assert.Equal("INV-123", membership.InvitationCode);
    }

    [Fact]
    public void CreateWithInvitation_ShouldRaise_HouseholdMemberInvitedEvent()
    {
        var membership = HouseholdMembership.CreateWithInvitation(HouseholdId.New(), UserId.New(), "INV-1");
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberInvited>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void ChangeRole_ShouldUpdateRole_AndRaiseEvent()
    {
        var membership = CreateActiveMembership(HouseholdRole.Member);
        membership.ClearDomainEvents();

        membership.ChangeRole(HouseholdRole.Owner);

        Assert.Equal(HouseholdRole.Owner, membership.Role);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberRoleChanged>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void ChangeRole_SameRole_ShouldThrow()
    {
        var membership = CreateActiveMembership(HouseholdRole.Member);
        Assert.Throws<InvalidOperationException>(() => membership.ChangeRole(HouseholdRole.Member));
    }

    [Fact]
    public void Remove_ShouldSetInactive_AndRaiseEvent()
    {
        var membership = CreateActiveMembership();
        membership.ClearDomainEvents();

        membership.Remove();

        Assert.False(membership.IsActive);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberRemoved>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void Remove_WhenAlreadyInactive_ShouldThrow()
    {
        var membership = CreateActiveMembership();
        membership.Remove();
        Assert.Throws<InvalidOperationException>(() => membership.Remove());
    }

    [Fact]
    public void AcceptInvitation_ShouldActivateMembership_AndClearCode()
    {
        // Arrange
        var membership = HouseholdMembership.CreateWithInvitation(HouseholdId.New(), UserId.New(), "INV-1");
        membership.ClearDomainEvents();
        var joiningUser = UserId.New();

        // Act
        membership.AcceptInvitation(joiningUser);

        // Assert
        Assert.True(membership.IsActive);
        Assert.Equal(joiningUser, membership.UserId);
        Assert.Null(membership.InvitationCode);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberJoined>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void AcceptInvitation_WhenAlreadyActive_ShouldThrow()
    {
        var membership = CreateActiveMembership();
        Assert.Throws<InvalidOperationException>(() => membership.AcceptInvitation(UserId.New()));
    }
}

public class IncomeSourceTests
{
    private static IncomeSource CreateIncome(decimal amount = 1000m)
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        return IncomeSource.Create(
            UserId.New(),
            Money.Create(amount, "USD"),
            "Salary",
            schedule);
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var userId = UserId.New();
        var amount = Money.Create(2500m, "USD");
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        // Act
        var income = IncomeSource.Create(userId, amount, "Contracting", schedule);

        // Assert
        Assert.Equal(userId, income.UserId);
        Assert.Equal(2500m, income.Amount.Amount);
        Assert.Equal("Contracting", income.Source);
        Assert.True(income.IsActive);
    }

    [Fact]
    public void Create_EmptySource_ShouldThrow()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        Assert.Throws<ArgumentException>(() =>
            IncomeSource.Create(UserId.New(), Money.Create(100m, "USD"), "  ", schedule));
    }

    [Fact]
    public void Create_ShouldRaise_IncomeSourceCreatedEvent()
    {
        var income = CreateIncome();
        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceCreated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldChangeAmountSourceAndSchedule()
    {
        // Arrange
        var income = CreateIncome();
        income.ClearDomainEvents();
        var newSchedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, new DateTime(2024, 6, 1));

        // Act
        income.Update(Money.Create(3000m, "USD"), "NewSource", newSchedule);

        // Assert
        Assert.Equal(3000m, income.Amount.Amount);
        Assert.Equal("NewSource", income.Source);
        Assert.Equal(RecurrenceFrequency.Weekly, income.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Update_ShouldRaise_IncomeSourceUpdatedEvent()
    {
        var income = CreateIncome();
        income.ClearDomainEvents();
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        income.Update(Money.Create(1200m, "USD"), "Salary", schedule);

        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceUpdated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptySource_ShouldThrow()
    {
        var income = CreateIncome();
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        Assert.Throws<ArgumentException>(() =>
            income.Update(Money.Create(1m, "USD"), "", schedule));
    }

    [Fact]
    public void Deactivate_ShouldSetInactive_AndRaiseEvent()
    {
        var income = CreateIncome();
        income.ClearDomainEvents();

        income.Deactivate();

        Assert.False(income.IsActive);
        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceDeactivated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var income = CreateIncome();
        income.Deactivate();
        Assert.Throws<InvalidOperationException>(() => income.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        var income = CreateIncome();

        var result = income.TryDeactivate();

        Assert.True(result);
        Assert.False(income.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        var income = CreateIncome();
        income.Deactivate();

        var result = income.TryDeactivate();

        Assert.False(result);
    }

    [Fact]
    public void Activate_WhenInactive_ShouldSetActive()
    {
        var income = CreateIncome();
        income.Deactivate();

        income.Activate();

        Assert.True(income.IsActive);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrow()
    {
        var income = CreateIncome();
        Assert.Throws<InvalidOperationException>(() => income.Activate());
    }
}

public class PersonalBillTests
{
    private static PersonalBill CreateValidPersonalBill(
        UserId? userId = null,
        decimal amount = 75m,
        BillCategory category = BillCategory.Utilities,
        string title = "Phone Bill",
        RecurrenceSchedule? schedule = null)
    {
        return PersonalBill.Create(
            userId ?? UserId.New(),
            title,
            Money.Create(amount, "USD"),
            category,
            DateTime.UtcNow.Date.AddDays(3),
            schedule);
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var userId = UserId.New();
        var dueDate = DateTime.UtcNow.Date.AddDays(7);
        var amount = Money.Create(120m, "USD");

        // Act
        var bill = PersonalBill.Create(userId, "Netflix", amount, BillCategory.Other, dueDate, description: "Streaming");

        // Assert
        Assert.Equal(userId, bill.UserId);
        Assert.Equal("Netflix", bill.Title);
        Assert.Equal(120m, bill.Amount.Amount);
        Assert.Equal(BillCategory.Other, bill.Category);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.Equal("Streaming", bill.Description);
        Assert.True(bill.IsActive);
        Assert.Null(bill.RecurrenceSchedule);
    }

    [Fact]
    public void Create_ShouldRaise_PersonalBillCreatedEvent()
    {
        // Arrange / Act
        var bill = CreateValidPersonalBill();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<PersonalBillCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() =>
            PersonalBill.Create(UserId.New(), "  ", Money.Create(50m, "USD"), BillCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(-10m, "USD"));
    }

    [Fact]
    public void Create_WithRecurrenceSchedule_ShouldSetSchedule()
    {
        // Arrange
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        // Act
        var bill = CreateValidPersonalBill(schedule: schedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Monthly, bill.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Update_ShouldChangeTitleAmountCategoryAndDueDate()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        // Act
        bill.Update("Updated Bill", Money.Create(200m, "USD"), BillCategory.Rent, newDueDate, description: "New desc");

        // Assert
        Assert.Equal("Updated Bill", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(BillCategory.Rent, bill.Category);
        Assert.Equal(newDueDate, bill.DueDate);
        Assert.Equal("New desc", bill.Description);
    }

    [Fact]
    public void Update_ShouldRaise_PersonalBillUpdatedEvent()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        bill.ClearDomainEvents();

        // Act
        bill.Update("New Title", Money.Create(50m, "USD"), BillCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<PersonalBillUpdated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidPersonalBill();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            bill.Update("", Money.Create(50m, "USD"), BillCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Update_WithRecurrenceSchedule_ShouldUpdateSchedule()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        var newSchedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, new DateTime(2024, 6, 1));

        // Act
        bill.Update("Title", Money.Create(50m, "USD"), BillCategory.Other, DateTime.UtcNow.Date.AddDays(1), recurrenceSchedule: newSchedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Weekly, bill.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var bill = CreateValidPersonalBill();

        // Act
        bill.Deactivate();

        // Assert
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldRaise_PersonalBillDeactivatedEvent()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        bill.ClearDomainEvents();

        // Act
        bill.Deactivate();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<PersonalBillDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        // Arrange
        var bill = CreateValidPersonalBill();

        // Act
        var result = bill.TryDeactivate();

        // Assert
        Assert.True(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        bill.Deactivate();

        // Act
        var result = bill.TryDeactivate();

        // Assert
        Assert.False(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEvents()
    {
        // Arrange
        var bill = CreateValidPersonalBill();
        Assert.NotEmpty(bill.GetDomainEvents());

        // Act
        bill.ClearDomainEvents();

        // Assert
        Assert.Empty(bill.GetDomainEvents());
    }
}
