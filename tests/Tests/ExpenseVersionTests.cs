using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The version is what stops two people re-cutting one bill at once. Shares are their own rows, so
/// "they must not exceed the total" is read-then-write: both writers see the same total, both fit,
/// and the sum lands above it. Every write that could break that moves the version, so the second
/// one is rejected instead.
/// </summary>
public class ExpenseVersionTests
{
    private static Expense Rent() => Expense.Create(
        AccountingEntity.Household(Guid.NewGuid()), UserId.New(), "Rent",
        Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date);

    [Fact]
    public void AShareChanging_MovesTheVersion()
    {
        var expense = Rent();
        var before = expense.Version;

        expense.RecordShareChange();

        Assert.Equal(before + 1, expense.Version);
    }

    // The total is what the shares have to fit inside, so changing it has to be serialised against
    // them too — otherwise shrinking a bill races a share being added and strands the shares above it.
    [Fact]
    public void TheTotalChanging_MovesTheVersionAsWell()
    {
        var expense = Rent();
        var before = expense.Version;

        expense.Update("Rent", Money.Create(700m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date);

        Assert.Equal(before + 1, expense.Version);
    }

    [Fact]
    public void EverySuccessiveChange_MovesItAgain()
    {
        var expense = Rent();

        expense.RecordShareChange();
        expense.RecordShareChange();
        expense.RecordShareChange();

        Assert.Equal(3u, expense.Version);
    }

    // Recording a share change is bookkeeping about the expense, not a fact about it — the share
    // raises its own event, and a second one here would post the same movement twice.
    [Fact]
    public void RecordingAShareChange_RaisesNoEvent()
    {
        var expense = Rent();
        expense.ClearDomainEvents();

        expense.RecordShareChange();

        Assert.Empty(expense.GetDomainEvents());
    }
}
