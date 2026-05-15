using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Income sources — covers both group-scoped (api/finance/groups/{groupId}/income)
/// and user-scoped (api/finance/income) routes. Both are driven by the same IncomeSource aggregate.
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/groups/{groupId:guid}/income")]
public sealed class IncomeController : ControllerBase
{
    private readonly IIncomeManager _manager;
    private readonly IIncomeQuery _incomeQuery;

    public IncomeController(IIncomeManager manager, IIncomeQuery incomeQuery)
    {
        _manager = manager;
        _incomeQuery = incomeQuery;
    }

    // ── Group-scoped income ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _incomeQuery.ListAsync(new ListIncomeParams(groupId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{incomeId:guid}")]
    public async Task<IActionResult> GetDetail(Guid groupId, Guid incomeId, CancellationToken ct = default)
    {
        var result = await _incomeQuery.GetDetailAsync(new IncomeDetailParams(incomeId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid groupId, [FromBody] CreateIncomeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { groupId, incomeId = result.IncomeId }, result);
    }

    [HttpPut("{incomeId:guid}")]
    public async Task<IActionResult> Update(Guid groupId, Guid incomeId, [FromBody] UpdateIncomeCommand request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{incomeId:guid}")]
    public async Task<IActionResult> Deactivate(Guid groupId, Guid incomeId, CancellationToken ct = default)
    {
        var result = await _manager.DeactivateAsync(new DeactivateIncomeCommand(incomeId), ct);
        return result is null ? NotFound() : NoContent();
    }

    // ── User-scoped income ────────────────────────────────────────────────────

    [HttpGet("/api/finance/income")]
    public async Task<IActionResult> ListByUser([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _incomeQuery.ListByUserAsync(new ListUserIncomeParams(userId.Value, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpPost("/api/finance/income")]
    public async Task<IActionResult> CreateForUser([FromBody] CreateIncomeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(ListByUser), result);
    }

    [HttpPut("/api/finance/income/{incomeId:guid}")]
    public async Task<IActionResult> UpdateForUser(Guid incomeId, [FromBody] UpdateIncomeCommand request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("/api/finance/income/{incomeId:guid}")]
    public async Task<IActionResult> DeactivateForUser(Guid incomeId, CancellationToken ct = default)
    {
        var result = await _manager.DeactivateAsync(new DeactivateIncomeCommand(incomeId), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPut("/api/finance/income/{incomeId:guid}/tax-profile")]
    public async Task<IActionResult> SetTaxProfile(Guid incomeId, [FromBody] SetTaxProfileCommand request, CancellationToken ct = default)
    {
        var result = await _manager.SetTaxProfileAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("/api/finance/income/{incomeId:guid}/deductions")]
    public async Task<IActionResult> AddDeduction(Guid incomeId, [FromBody] AddDeductionCommand request, CancellationToken ct = default)
    {
        var result = await _manager.AddDeductionAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("/api/finance/income/{incomeId:guid}/deductions")]
    public async Task<IActionResult> RemoveDeduction(Guid incomeId, [FromBody] RemoveDeductionCommand request, CancellationToken ct = default)
    {
        var result = await _manager.RemoveDeductionAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("/api/finance/income/{incomeId:guid}/net-pay")]
    public async Task<IActionResult> GetNetPayBreakdown(Guid incomeId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var result = await _incomeQuery.GetNetPayBreakdownAsync(
            new GetNetPayBreakdownParams(incomeId, year ?? now.Year, month ?? now.Month), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Returns the full per-month contribution/budget summary for the authenticated user.
    /// Covers projected income, household split obligations, personal bill obligations and
    /// (where available) real-balance disposable income.
    /// </summary>
    [HttpGet("/api/finance/contribution-summary")]
    public async Task<IActionResult> GetContributionSummary(
        [FromQuery] int months = 13,
        [FromQuery] int past = 3,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _incomeQuery.GetContributionSummariesAsync(userId.Value, DateTime.UtcNow, months, past, ct);
        return Ok(result);
    }
}
