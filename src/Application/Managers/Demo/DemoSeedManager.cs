using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers.Demo;

internal sealed class DemoSeedManager : IDemoSeedManager
{
    private readonly IIncomeSourceRepository _incomeRepo;
    private readonly IChargeRepository _chargeRepo;
    private readonly IAllocationRepository _allocationRepo;

    public DemoSeedManager(
        IIncomeSourceRepository incomeRepo,
        IChargeRepository chargeRepo,
        IAllocationRepository allocationRepo)
    {
        _incomeRepo = incomeRepo;
        _chargeRepo = chargeRepo;
        _allocationRepo = allocationRepo;
    }

    public async Task SeedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var salary = IncomeSource.Create(
            uid,
            Money.Create(5000m, "USD"),
            "Full-time Employment",
            RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth),
            paymentFrequency: RecurrenceFrequency.BiWeekly,
            lastPaymentDate: now.AddDays(-7));
        await _incomeRepo.AddAsync(salary, cancellationToken);

        var expenses = new[]
        {
            Charge.CreateOwn(
                uid, "Rent", Money.Create(1500m, "USD"),
                ChargeCategory.Rent, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Internet", Money.Create(60m, "USD"),
                ChargeCategory.Internet, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Spotify", Money.Create(11m, "USD"),
                ChargeCategory.Subscriptions, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Phone Plan", Money.Create(45m, "USD"),
                ChargeCategory.Phone, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Health Insurance", Money.Create(200m, "USD"),
                ChargeCategory.Insurance, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Gym Membership", Money.Create(25m, "USD"),
                ChargeCategory.Healthcare, startOfMonth.AddMonths(1)),
            Charge.CreateOwn(
                uid, "Car Insurance", Money.Create(110m, "USD"),
                ChargeCategory.Insurance, startOfMonth.AddMonths(1)),
        };
        foreach (var charge in expenses)
            await _chargeRepo.AddAsync(charge, cancellationToken);

        await _incomeRepo.CommitAsync(cancellationToken);
        await _chargeRepo.CommitAsync(cancellationToken);
    }

    public async Task SeedGroupChargesAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        var gid = new GroupId(groupId);
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var sharedCharges = new[]
        {
            (title: "Electricity", amount: 120m, category: ChargeCategory.Utilities),
            (title: "Groceries", amount: 400m, category: ChargeCategory.Groceries),
            (title: "Water & Gas", amount: 80m, category: ChargeCategory.Utilities),
            (title: "Netflix", amount: 18m, category: ChargeCategory.Subscriptions),
        };

        // Seeding goes through the outbox like any live write, so demo households get a real double-entry
        // ledger by the same path — seeded data is never ledger-less.
        foreach (var (title, amount, category) in sharedCharges)
        {
            // A PayerMember charge has to name the member who fronted it — the default left
            // payerUserId null, so the bill detail rendered "Someone, out of their own pocket" and
            // there was nobody for the house to pay back.
            var charge = Charge.Create(
                AccountingEntity.Household(gid), uid, title, Money.Create(amount, "USD"),
                category, startOfMonth.AddMonths(1),
                payerUserId: userId);
            await _chargeRepo.AddAsync(charge, cancellationToken);

            var allocation = Allocation.Create(charge, uid, Money.Create(amount, "USD"));
            await _allocationRepo.AddAsync(allocation, cancellationToken);
        }

        await _chargeRepo.CommitAsync(cancellationToken);
    }

    public async Task CleanupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        await _allocationRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _incomeRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _chargeRepo.DeleteAllForUserAsync(uid, cancellationToken);
    }
}
