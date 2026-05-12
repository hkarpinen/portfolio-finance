using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Household (shared) expenses — scoped to a household.
/// Route: api/finance/households/{householdId}/expenses
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/households/{householdId:guid}/expenses")]
public sealed class HouseholdExpensesController : ControllerBase
{
    private readonly IExpenseManager _manager;
    private readonly IExpenseQuery _query;

    public HouseholdExpensesController(IExpenseManager manager, IExpenseQuery query)
    {
        _manager = manager;
        _query = query;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid householdId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByHouseholdAsync(new ListHouseholdExpensesParams(householdId, page, pageSize, ActiveOnly: true, CallerId: userId), ct);
        return Ok(result);
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<IActionResult> GetDetail(Guid householdId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetHouseholdDetailAsync(new HouseholdExpenseDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{expenseId:guid}/detail")]
    public async Task<IActionResult> GetFullDetail(Guid householdId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.GetHouseholdExpenseDetailAsync(expenseId, userId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid householdId, [FromBody] CreateHouseholdExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateHouseholdExpenseAsync(request with { HouseholdId = householdId, CreatedBy = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { householdId, expenseId = result.ExpenseId }, result);
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid expenseId, [FromBody] UpdateHouseholdExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.UpdateHouseholdExpenseAsync(request with { ExpenseId = expenseId, CallerId = userId.Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> Deactivate(Guid householdId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.DeactivateHouseholdExpenseAsync(new DeactivateExpenseCommand(expenseId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPost("{expenseId:guid}/payments")]
    public async Task<IActionResult> PaySplit(Guid householdId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkExpensePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}/payments")]
    public async Task<IActionResult> UnpaySplit(Guid householdId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkExpenseUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Split management ─────────────────────────────────────────────────────

    [HttpGet("{expenseId:guid}/splits")]
    public async Task<IActionResult> ListSplits(Guid householdId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.ListSplitsAsync(new ListSplitsParams(expenseId), ct);
        return Ok(result);
    }

    [HttpPost("{expenseId:guid}/splits")]
    public async Task<IActionResult> UpsertSplit(Guid householdId, Guid expenseId, [FromBody] UpsertSplitCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        try
        {
            var result = await _manager.UpsertSplitAsync(
                request with { ExpenseId = expenseId, HouseholdId = householdId, UserId = userId.Value }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{expenseId:guid}/splits/even")]
    public async Task<IActionResult> SplitEvenly(Guid householdId, Guid expenseId, [FromBody] SplitEvenlyBody body, CancellationToken ct = default)
    {
        await _manager.SplitEvenlyAsync(expenseId, body.MembershipIds, ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}/splits/{splitId:guid}")]
    public async Task<IActionResult> RemoveSplit(Guid householdId, Guid expenseId, Guid splitId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.RemoveSplitAsync(new RemoveSplitCommand(splitId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }
}
