using Bills.Application.Contracts;
using Bills.Application.Managers;
using Bills.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/bills/households/{householdId:guid}/bills/{billId:guid}/splits")]
public sealed class SplitsController : ControllerBase
{
    private readonly IBillWorkflowManager _manager;
    private readonly IBillQuery _billQuery;

    public SplitsController(IBillWorkflowManager manager, IBillQuery billQuery)
    {
        _manager = manager;
        _billQuery = billQuery;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid householdId, Guid billId, CancellationToken ct = default)
    {
        var result = await _billQuery.ListSplitsAsync(new ListSplitsRequest(billId), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert(Guid householdId, Guid billId, [FromBody] UpsertSplitRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        try
        {
            var result = await _manager.UpsertSplitAsync(
                request with { BillId = billId, HouseholdId = householdId, UserId = userId.Value }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{splitId:guid}")]
    public async Task<IActionResult> Remove(Guid householdId, Guid billId, Guid splitId, CancellationToken ct = default)
    {
        var result = await _manager.RemoveSplitAsync(new RemoveSplitRequest(splitId), ct);
        return result is null ? NotFound() : NoContent();
    }
}

