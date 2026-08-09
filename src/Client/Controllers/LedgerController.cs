using Finance.Application.Queries;
using Client.Extensions;
using Client.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

// Group-scoped by opaque GroupId — finance knows nothing of "household". Read-only over HTTP:
// the ledger is never event-replicated out.
[ApiController]
[Authorize]
[RequireGroupMembership]
[EnableRateLimiting("api")]
[Route("api/finance/groups/{groupId:guid}/ledger")]
public sealed class LedgerController : ControllerBase
{
    private readonly ILedgerQuery _query;

    public LedgerController(ILedgerQuery query) => _query = query;

    [HttpGet]
    public async Task<IActionResult> GetGroupLedger(Guid groupId, CancellationToken ct = default)
    {
        var ledger = await _query.GetGroupLedgerAsync(groupId, ct);
        return ledger is null ? NotFound() : Ok(ledger);
    }

    // Absolute route: it hangs off /accounts, not /ledger.
    [HttpGet("/api/finance/groups/{groupId:guid}/accounts/{accountId:guid}/statement")]
    public async Task<IActionResult> GetAccountStatement(Guid groupId, Guid accountId, CancellationToken ct = default)
    {
        var statement = await _query.GetAccountStatementAsync(groupId, accountId, ct);
        return statement is null ? NotFound() : Ok(statement);
    }

    // Absolute route because it is user-scoped, not group-scoped — which also means the class-level
    // membership filter does not apply to it.
    [HttpGet("/api/finance/me/position")]
    public async Task<IActionResult> GetMyPosition(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var position = await _query.GetUserPositionAsync(userId.Value, ct);
        return Ok(position);
    }
}
