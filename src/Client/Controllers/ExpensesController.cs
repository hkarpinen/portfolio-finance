using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Personal expenses — owned by a single user, not shared with any household.
/// Route: api/finance/expenses
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
}
