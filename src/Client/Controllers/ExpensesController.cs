using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Expenses — covers both personal (api/finance/expenses) and group-scoped
/// (api/finance/groups/{groupId}/expenses) routes. Both are driven by the same Expense aggregate.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseManager _manager;
    private readonly IExpenseQuery _query;

    public ExpensesController(IExpenseManager manager, IExpenseQuery query)
    {
        _manager = manager;
        _query = query;
    }

    // ── Personal expenses ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByUserAsync(new ListExpensesParams(userId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<IActionResult> GetDetail(Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetDetailAsync(new ExpenseDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { expenseId = result.ExpenseId }, result);
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<IActionResult> Update(Guid expenseId, [FromBody] UpdateExpenseCommand request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { ExpenseId = expenseId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> Delete(Guid expenseId, CancellationToken ct = default)
    {
        var result = await _manager.DeleteAsync(new DeleteExpenseCommand(expenseId), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPost("{expenseId:guid}/payments")]
    public async Task<IActionResult> MarkPaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkExpensePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}/payments")]
    public async Task<IActionResult> MarkUnpaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkExpenseUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Household expenses ────────────────────────────────────────────────────

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses")]
    public async Task<IActionResult> ListByHousehold(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByHouseholdAsync(
            new ListHouseholdExpensesParams(groupId, page, pageSize, ActiveOnly: true, CallerId: userId), ct);
        return Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> GetHouseholdDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetHouseholdDetailAsync(new HouseholdExpenseDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/detail")]
    public async Task<IActionResult> GetHouseholdFullDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.GetHouseholdExpenseDetailAsync(expenseId, userId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses")]
    public async Task<IActionResult> CreateHousehold(Guid groupId, [FromBody] CreateHouseholdExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateHouseholdExpenseAsync(
            request with { HouseholdId = groupId, CreatedBy = userId.Value }, ct);
        return CreatedAtAction(nameof(GetHouseholdDetail), new { groupId, expenseId = result.ExpenseId }, result);
    }

    [HttpPut("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> UpdateHousehold(Guid groupId, Guid expenseId, [FromBody] UpdateHouseholdExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.UpdateHouseholdExpenseAsync(
            request with { ExpenseId = expenseId, CallerId = userId.Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeactivateHousehold(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.DeactivateHouseholdExpenseAsync(
            new DeactivateExpenseCommand(expenseId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    public async Task<IActionResult> PaySplit(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkExpensePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    public async Task<IActionResult> UnpaySplit(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkExpenseUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Splits ────────────────────────────────────────────────────────────────

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    public async Task<IActionResult> ListSplits(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.ListSplitsAsync(new ListSplitsParams(expenseId), ct);
        return Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    public async Task<IActionResult> UpsertSplit(Guid groupId, Guid expenseId, [FromBody] UpsertSplitCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        try
        {
            var result = await _manager.UpsertSplitAsync(
                request with { ExpenseId = expenseId, GroupId = groupId, UserId = userId.Value }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/even")]
    public async Task<IActionResult> SplitEvenly(Guid groupId, Guid expenseId, [FromBody] SplitEvenlyBody body, CancellationToken ct = default)
    {
        await _manager.SplitEvenlyAsync(expenseId, body.UserIds, ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/{splitId:guid}")]
    public async Task<IActionResult> RemoveSplit(Guid groupId, Guid expenseId, Guid splitId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.RemoveSplitAsync(new RemoveSplitCommand(splitId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-month, per-member contribution breakdowns for a household.
    /// Window: 3 past months + current month + 8 future months (12 total).
    /// </summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/contributions")]
    public async Task<IActionResult> GetContributions(Guid groupId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var windowEnd = windowStart.AddMonths(12).AddDays(-1);
        var result = await _query.ListSplitsByHouseholdAsync(
            GroupId.Create(groupId), windowStart, windowEnd, ct);
        var nonEmpty = result.Where(m => m.Members.Count > 0).ToList();
        return Ok(nonEmpty);
    }
}
