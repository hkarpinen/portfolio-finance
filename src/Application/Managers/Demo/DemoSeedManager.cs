using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers.Demo;

internal sealed class DemoSeedManager : IDemoSeedManager
{
    private readonly IIncomeSourceRepository _incomeRepo;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IExpenseSplitRepository _splitRepo;

    public DemoSeedManager(
        IIncomeSourceRepository incomeRepo,
        IExpenseRepository expenseRepo,
        IExpenseSplitRepository splitRepo)
    {
        _incomeRepo = incomeRepo;
        _expenseRepo = expenseRepo;
        _splitRepo = splitRepo;
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
            Expense.Create(uid, "Rent", Money.Create(1500m, "USD"),
                ExpenseCategory.Rent, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Internet", Money.Create(60m, "USD"),
                ExpenseCategory.Internet, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Spotify", Money.Create(11m, "USD"),
                ExpenseCategory.Subscriptions, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Phone Plan", Money.Create(45m, "USD"),
                ExpenseCategory.Phone, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Health Insurance", Money.Create(200m, "USD"),
                ExpenseCategory.Insurance, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Gym Membership", Money.Create(25m, "USD"),
                ExpenseCategory.Healthcare, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
            Expense.Create(uid, "Car Insurance", Money.Create(110m, "USD"),
                ExpenseCategory.Insurance, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth)),
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

        var sharedExpenses = new[]
        {
            (title: "Electricity", amount: 120m, category: ExpenseCategory.Utilities),
            (title: "Groceries", amount: 400m, category: ExpenseCategory.Groceries),
            (title: "Water & Gas", amount: 80m, category: ExpenseCategory.Utilities),
            (title: "Netflix", amount: 18m, category: ExpenseCategory.Subscriptions),
        };

        foreach (var (title, amount, category) in sharedExpenses)
        {
            var expense = Expense.CreateHousehold(
                gid, uid, title, Money.Create(amount, "USD"),
                category, startOfMonth.AddMonths(1),
                RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, startOfMonth));
            await _expenseRepo.AddAsync(expense, cancellationToken);

            var split = ExpenseSplit.Create(expense.Id, gid, uid, Money.Create(amount, "USD"));
            await _splitRepo.AddAsync(split, cancellationToken);
        }

        await _expenseRepo.CommitAsync(cancellationToken);
    }

    public async Task CleanupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = new UserId(userId);
        await _splitRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _incomeRepo.DeleteAllForUserAsync(uid, cancellationToken);
        await _expenseRepo.DeleteAllForUserAsync(uid, cancellationToken);
    }
}
