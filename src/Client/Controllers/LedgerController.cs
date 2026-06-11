using Finance.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

/// <summary>
/// Read access to a group's double-entry ledger. Group-scoped by opaque GroupId —
/// finance knows nothing of "household". This is the cross-boundary read surface
/// household/frontend pull from; the ledger is never event-replicated out.
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/finance/groups/{groupId:guid}/ledger")]
public sealed class LedgerController : ControllerBase
{
    private readonly ILedgerQuery _query;

    public LedgerController(ILedgerQuery query) => _query = query;

    /// <summary>The group's chart of accounts with derived balances + the trial balance.</summary>
    [HttpGet]
    public async Task<IActionResult> GetGroupLedger(Guid groupId, CancellationToken ct = default)
    {
        var ledger = await _query.GetGroupLedgerAsync(groupId, ct);
        return ledger is null ? NotFound() : Ok(ledger);
    }

    /// <summary>One account's statement — its postings with a running balance. The drill-in behind
    /// an account row in the ledger view. Absolute route (it hangs off /accounts, not /ledger).</summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/accounts/{accountId:guid}/statement")]
    public async Task<IActionResult> GetAccountStatement(Guid groupId, Guid accountId, CancellationToken ct = default)
    {
        var statement = await _query.GetAccountStatementAsync(groupId, accountId, ct);
        return statement is null ? NotFound() : Ok(statement);
    }

    /// <summary>The caller's net financial position across every group they're in — derived from the
    /// group ledgers' member-equity accounts (no separate user ledger). Powers the personal home.
    /// Absolute route — it's user-scoped, not group-scoped.</summary>
    [HttpGet("/api/finance/me/position")]
    public async Task<IActionResult> GetMyPosition(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var position = await _query.GetUserPositionAsync(userId.Value, ct);
        return Ok(position);
    }
}
