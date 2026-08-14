using Client.Extensions;
using Client.Filters;
using Finance.Application.Dtos;
using Finance.Application.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

/// <summary>
/// Repeating costs — the agreement, not the individual bills.
///
/// A schedule posts nothing. It says which expenses should exist; the forecast every screen draws
/// is <c>occurrences</c>, and an expense is written only when somebody acts on one.
/// </summary>
[ApiController]
[Authorize]
[RequireGroupMembership]
[Route("api/finance/recurring-expenses")]
public sealed class RecurringExpensesController(
    IRecurringExpenseManager schedules) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken ct = default)
        => Ok(await schedules.ListForUserAsync(User.GetUserId().Value, ct));

    [HttpGet("/api/finance/groups/{groupId:guid}/schedules")]
    public async Task<IActionResult> ListForGroup(Guid groupId, CancellationToken ct = default)
        => Ok(await schedules.ListForGroupAsync(groupId, ct));

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreateRecurringExpenseCommand request, CancellationToken ct = default)
    {
        var result = await schedules.CreateAsync(request with { CallerUserId = User.GetUserId().Value }, ct);
        return CreatedAtAction(nameof(ListMine), new { }, result);
    }

    [HttpPut("{recurringExpenseId:guid}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Amend(Guid recurringExpenseId, [FromBody] AmendRecurringExpenseCommand request, CancellationToken ct = default)
    {
        var result = await schedules.AmendAsync(
            request with { RecurringExpenseId = recurringExpenseId, CallerUserId = User.GetUserId().Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{recurringExpenseId:guid}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Deactivate(Guid recurringExpenseId, CancellationToken ct = default)
        => await schedules.DeactivateAsync(recurringExpenseId, User.GetUserId().Value, ct) ? NoContent() : NotFound();

    /// <summary>
    /// The dates this schedule places an expense on, and the expense for each where one exists.
    /// Most will be null — that is the forecast, and nothing is written until somebody acts.
    /// </summary>
    [HttpGet("{recurringExpenseId:guid}/occurrences")]
    public async Task<IActionResult> Occurrences(
        Guid recurringExpenseId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        if (to <= from) return BadRequest(new { error = "'to' must be after 'from'." });
        return Ok(await schedules.ForecastAsync(recurringExpenseId, from, to, ct));
    }

    /// <summary>
    /// Writes every occurrence that has come due and is not recorded yet. What turns a period
    /// passing into an expense: the money screens call it before they read, so by the time anyone
    /// looks, the bills that came due are on the books.
    /// </summary>
    [HttpPost("catch-up")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> CatchUpMine(CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;

        var (generated, posted) = await schedules.CatchUpPersonalAsync(userId, DateTime.UtcNow, ct);

        return Ok(new { generated, posted });
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/schedules/catch-up")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> CatchUpForGroup(Guid groupId, CancellationToken ct = default)
        => Ok(new { generated = await schedules.CatchUpAsync(groupId, User.GetUserId().Value, DateTime.UtcNow, ct) });

    /// <summary>Writes the expense for one occurrence. Idempotent — the second call returns the first.</summary>
    [HttpPost("{recurringExpenseId:guid}/occurrences/{occurrenceDate:datetime}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Materialise(Guid recurringExpenseId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var expense = await schedules.MaterialiseAsync(recurringExpenseId, occurrenceDate, ct);
        return expense is null
            ? NotFound()
            : Ok(new { expenseId = expense.Id.Value, occurrenceDate = expense.OccurrenceDate, amount = expense.Amount.Amount });
    }
}
