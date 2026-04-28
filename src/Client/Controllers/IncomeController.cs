using Bills.Application.Contracts;
using Bills.Application.Managers;
using Bills.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/bills/households/{householdId:guid}/income")]
public sealed class IncomeController : ControllerBase
{
    private readonly IIncomeManager _manager;
    private readonly IIncomeQuery _incomeQuery;

    public IncomeController(IIncomeManager manager, IIncomeQuery incomeQuery)
    {
        _manager = manager;
        _incomeQuery = incomeQuery;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid householdId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _incomeQuery.ListAsync(new ListIncomeRequest(householdId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{incomeId:guid}")]
    public async Task<IActionResult> GetDetail(Guid householdId, Guid incomeId, CancellationToken ct = default)
    {
        var result = await _incomeQuery.GetDetailAsync(new IncomeDetailRequest(incomeId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid householdId, [FromBody] CreateIncomeRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { householdId, incomeId = result.IncomeId }, result);
    }

    [HttpPut("{incomeId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid incomeId, [FromBody] UpdateIncomeRequest request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { IncomeId = incomeId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{incomeId:guid}")]
    public async Task<IActionResult> Deactivate(Guid householdId, Guid incomeId, CancellationToken ct = default)
    {
        var result = await _manager.DeactivateAsync(new DeactivateIncomeRequest(incomeId), ct);
        return result is null ? NotFound() : NoContent();
    }
}

