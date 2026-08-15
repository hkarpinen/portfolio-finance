using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Tests;

/// <summary>
/// That the queries this service runs can actually be turned into SQL.
///
/// `Expense.GroupId` is `=> Owner.AsGroup`. It reads like a column and compiles like one, so
/// `.Where(e => e.GroupId == g)` is accepted by the compiler and then throws the moment it runs —
/// the page returns an EF translation error as its body. That shipped in five places at once:
/// the membership filter, the member-balances read, the household-deleted cascade, and both
/// recurring lists. Every one was invisible to the 264 tests beside this file, because none of
/// them goes near a query.
///
/// No database is involved. `ToQueryString()` builds the model and translates the expression,
/// which is the whole of what was broken — a connection string that points nowhere is enough.
/// That is what makes this affordable to keep: the harness here has no database, and this needs
/// none.
///
/// The rule these encode: filter on Owner.Kind and Owner.Id, which are real columns, never on the
/// GroupId shorthand.
/// </summary>
public class QueryTranslationTests
{
    private static FinanceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql("Host=translation-only;Database=none;Username=none;Password=none")
            .Options;

        return new FinanceDbContext(options);
    }

    private static readonly Guid AGroup = Guid.NewGuid();
    private static readonly Guid APerson = Guid.NewGuid();

    // The check the group-membership filter runs on every group route carrying an {expenseId}.
    [Fact]
    public void AnExpenseCanBeMatchedToItsGroup()
    {
        using var db = NewContext();
        var id = ExpenseId.Create(Guid.NewGuid());

        var sql = db.Expenses
            .Where(e => e.Id == id && e.Owner.Kind == EntityKind.Group && e.Owner.Id == AGroup)
            .ToQueryString();

        Assert.Contains("owner_kind", sql);
        Assert.Contains("owner_id", sql);
    }

    [Fact]
    public void AGroupsActiveExpensesCanBeListed()
    {
        using var db = NewContext();

        var sql = db.Expenses
            .Where(e => e.Owner.Kind == EntityKind.Group && e.Owner.Id == AGroup && e.IsActive)
            .ToQueryString();

        Assert.Contains("owner_kind", sql);
    }

    [Fact]
    public void AGroupsSchedulesCanBeListed()
    {
        using var db = NewContext();

        var sql = db.RecurringExpenses
            .Where(s => s.Owner.Kind == EntityKind.Group && s.Owner.Id == AGroup && s.IsActive)
            .ToQueryString();

        Assert.Contains("owner_kind", sql);
    }

    [Fact]
    public void APersonsOwnSchedulesCanBeListed()
    {
        using var db = NewContext();

        var sql = db.RecurringExpenses
            .Where(s => s.Owner.Kind == EntityKind.Person && s.Owner.Id == APerson && s.IsActive)
            .ToQueryString();

        Assert.Contains("owner_kind", sql);
    }

    /// <summary>
    /// Looking people up by id — every screen that shows a name beside a share runs this.
    ///
    /// UserId is value-converted, so the converted property translates and reaching THROUGH it
    /// does not. `userIds.Contains(p.UserId.Value)` compiled and threw, which is the same shape of
    /// mistake as the computed GroupId with a different cause: one member EF can map, one it
    /// cannot, and nothing between the two at compile time.
    /// </summary>
    [Fact]
    public void PeopleCanBeLookedUpByAListOfIds()
    {
        using var db = NewContext();
        var ids = new List<UserId> { UserId.Create(APerson) };

        // Producing SQL at all is the assertion — the broken form threw instead.
        var sql = db.UserProjections.Where(p => ids.Contains(p.UserId)).ToQueryString();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReachingThroughTheConvertedIdIsNotTranslatable()
    {
        using var db = NewContext();
        var raw = new List<Guid> { APerson };

        Assert.Throws<InvalidOperationException>(
            () => db.UserProjections.Where(p => raw.Contains(p.UserId.Value)).ToQueryString());
    }

    /// <summary>
    /// The bug itself, held in place. If someone reaches for the shorthand again — it is right
    /// there on the aggregate and reads perfectly — this says so here rather than in a page body.
    /// </summary>
    [Fact]
    public void FilteringOnTheComputedGroupIdIsNotTranslatable()
    {
        using var db = NewContext();
        var group = GroupId.Create(AGroup);

        var untranslatable = () => db.Expenses.Where(e => e.GroupId == group).ToQueryString();

        var thrown = Assert.Throws<InvalidOperationException>(untranslatable);
        Assert.Contains("could not be translated", thrown.Message);
    }

    [Fact]
    public void TheSameShorthandOnAScheduleIsAlsoNotTranslatable()
    {
        using var db = NewContext();
        var group = GroupId.Create(AGroup);

        Assert.Throws<InvalidOperationException>(
            () => db.RecurringExpenses.Where(s => s.GroupId == group).ToQueryString());
    }
}
