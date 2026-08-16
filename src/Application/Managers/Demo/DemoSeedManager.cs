using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers.Demo;

internal sealed class DemoSeedManager : IDemoSeedManager
{
    private readonly IIncomeSourceRepository _incomeRepo;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IShareRepository _shareRepo;

    public DemoSeedManager(
        IIncomeSourceRepository incomeRepo,
        IExpenseRepository expenseRepo,
        IShareRepository shareRepo)
    {
        _incomeRepo = incomeRepo;
        _expenseRepo = expenseRepo;
        _shareRepo = shareRepo;
    }

    public async Task SeedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // THIS month, not next. The money screens show the month as it stands and skip occurrences
        // later than today, so expenses dated a month ahead put a demo account in front of a page of
        // zeros — every category empty, nothing in "the last few things", and the one figure the
        // product exists to show reading nothing.
        var thisMonth = startOfMonth;

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
            Expense.CreateOwn(
                uid, "Rent", Money.Create(1500m, "USD"),
                ExpenseCategory.Rent, thisMonth),
            Expense.CreateOwn(
                uid, "Internet", Money.Create(60m, "USD"),
                ExpenseCategory.Internet, thisMonth),
            Expense.CreateOwn(
                uid, "Spotify", Money.Create(11m, "USD"),
                ExpenseCategory.Subscriptions, thisMonth),
            Expense.CreateOwn(
                uid, "Phone Plan", Money.Create(45m, "USD"),
                ExpenseCategory.Phone, thisMonth),
            Expense.CreateOwn(
                uid, "Health Insurance", Money.Create(200m, "USD"),
                ExpenseCategory.Insurance, thisMonth),
            Expense.CreateOwn(
                uid, "Gym Membership", Money.Create(25m, "USD"),
                ExpenseCategory.Healthcare, thisMonth),
            Expense.CreateOwn(
                uid, "Car Insurance", Money.Create(110m, "USD"),
                ExpenseCategory.Insurance, thisMonth),
        };
        foreach (var expense in expenses)
            await _expenseRepo.AddAsync(expense, cancellationToken);

        await _incomeRepo.CommitAsync(cancellationToken);
        await _expenseRepo.CommitAsync(cancellationToken);
    }

    public async Task SeedGroupExpensesAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        var gid = new GroupId(groupId);
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // THIS month, not next. The money screens show the month as it stands and skip occurrences
        // later than today, so expenses dated a month ahead put a demo account in front of a page of
        // zeros — every category empty, nothing in "the last few things", and the one figure the
        // product exists to show reading nothing.
        var thisMonth = startOfMonth;

        var sharedExpenses = new[]
        {
            (title: "Electricity", amount: 120m, category: ExpenseCategory.Utilities),
            (title: "Groceries", amount: 400m, category: ExpenseCategory.Groceries),
            (title: "Water & Gas", amount: 80m, category: ExpenseCategory.Utilities),
            (title: "Netflix", amount: 18m, category: ExpenseCategory.Subscriptions),
        };

        // Seeding goes through the outbox like any live write, so demo groups get a real double-entry
        // ledger by the same path — seeded data is never ledger-less.
        foreach (var (title, amount, category) in sharedExpenses)
        {
            // A PayerMember expense has to name the member who fronted it — the default left
            // payerUserId null, so the expense detail rendered "Someone, out of their own pocket" and
            // there was nobody for the group to pay back.
            var expense = Expense.Create(
                AccountingEntity.Group(gid), uid, title, Money.Create(amount, "USD"),
                category, thisMonth,
                payerUserId: userId);
            await _expenseRepo.AddAsync(expense, cancellationToken);

            var share = Share.Create(expense, uid, Money.Create(amount, "USD"));
            await _shareRepo.AddAsync(share, cancellationToken);
        }

        await _expenseRepo.CommitAsync(cancellationToken);
    }

    public async Task CleanupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        await _shareRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _incomeRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _expenseRepo.DeleteAllForUserAsync(uid, cancellationToken);
    }
}
