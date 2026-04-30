using Finance.Application.Contracts;
using Finance.Application.Managers;
using Finance.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/income")]
public sealed class UserIncomeController : ControllerBase
{
    private readonly IIncomeManager _manager;
    private readonly IIncomeQuery _incomeQuery;

    public UserIncomeController(IIncomeManager manager, IIncomeQuery incomeQuery)
    {
        _manager = manager;
        _incomeQuery = incomeQuery;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _incomeQuery.ListByUserAsync(new ListUserIncomeRequest(userId.Value, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIncomeRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(List), result);
    }

    [HttpPut("{incomeId:guid}")]
    public async Task<IActionResult> Update(Guid incomeId, [FromBody] UpdateIncomeRequest request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{incomeId:guid}")]
    public async Task<IActionResult> Deactivate(Guid incomeId, CancellationToken ct = default)
    {
        var result = await _manager.DeactivateAsync(new DeactivateIncomeRequest(incomeId), ct);
        return result is null ? NotFound() : NoContent();
    }

    // ── Payroll deductions ───────────────────────────────────────────────────

    [HttpPut("{incomeId:guid}/tax-profile")]
    public async Task<IActionResult> SetTaxProfile(Guid incomeId, [FromBody] SetTaxProfileRequest request, CancellationToken ct = default)
    {
        var result = await _manager.SetTaxProfileAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{incomeId:guid}/deductions")]
    public async Task<IActionResult> AddDeduction(Guid incomeId, [FromBody] AddDeductionRequest request, CancellationToken ct = default)
    {
        var result = await _manager.AddDeductionAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{incomeId:guid}/deductions")]
    public async Task<IActionResult> RemoveDeduction(Guid incomeId, [FromBody] RemoveDeductionRequest request, CancellationToken ct = default)
    {
        var result = await _manager.RemoveDeductionAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{incomeId:guid}/net-pay")]
    public async Task<IActionResult> GetNetPayBreakdown(Guid incomeId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var result = await _incomeQuery.GetNetPayBreakdownAsync(
            new GetNetPayBreakdownRequest(incomeId, year ?? now.Year, month ?? now.Month), ct);
        return result is null ? NotFound() : Ok(result);
    }
}

