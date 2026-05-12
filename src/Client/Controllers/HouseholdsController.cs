using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/households")]
public sealed class HouseholdsController : ControllerBase
{
    private readonly IHouseholdManager _manager;
    private readonly IHouseholdQuery _householdQuery;
    private readonly IExpenseQuery _expenseQuery;

    public HouseholdsController(
        IHouseholdManager manager,
        IHouseholdQuery householdQuery,
        IExpenseQuery expenseQuery)
    {
        _manager = manager;
        _householdQuery = householdQuery;
        _expenseQuery = expenseQuery;
    }

    // ── Household CRUD ────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _householdQuery.ListAsync(new ListHouseholdsParams(userId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _householdQuery.GetDetailAsync(new HouseholdDetailParams(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var result = await _householdQuery.GetPageAsync(id, userId, periodStart, periodEnd, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseholdCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { OwnerId = userId.Value }, ct);
        return CreatedAtAction(nameof(Get), new { id = result.HouseholdId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHouseholdCommand request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { HouseholdId = id }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        try
        {
            var result = await _manager.DeleteAsync(new DeleteHouseholdCommand(id, userId), ct);
            return result ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        try
        {
            var result = await _manager.TransferOwnershipAsync(new TransferHouseholdOwnershipCommand(id, body.NewOwnerId, userId), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/dashboard")]
    public async Task<IActionResult> Dashboard(Guid id, [FromQuery] DateTime? periodStart, [FromQuery] DateTime? periodEnd, CancellationToken ct = default)
    {
        var start = periodStart ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end   = periodEnd   ?? start.AddMonths(1).AddDays(-1);
        var result = await _householdQuery.QueryAsync(new DashboardParams(id, start, end), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/dashboard/coverage")]
    public async Task<IActionResult> DashboardCoverage(Guid id, [FromQuery] DateTime? periodStart, [FromQuery] DateTime? periodEnd, CancellationToken ct = default)
    {
        var start = periodStart ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end   = periodEnd   ?? start.AddMonths(1).AddDays(-1);
        var result = await _householdQuery.GetCoverageStatusAsync(new CoverageStatusParams(id, start, end), ct);
        return Ok(result);
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-month, per-member contribution breakdowns for a household.
    /// Window: 3 past months + current month + 8 future months (12 total).
    /// </summary>
    [HttpGet("{id:guid}/contributions")]
    public async Task<IActionResult> GetContributions(Guid id, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var windowEnd = windowStart.AddMonths(12).AddDays(-1);
        var result = await _expenseQuery.ListSplitsByHouseholdAsync(
            HouseholdId.Create(id), windowStart, windowEnd, ct);
        var nonEmpty = result.Where(m => m.Members.Count > 0).ToList();
        return Ok(nonEmpty);
    }

    // ── Members ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken ct = default)
    {
        var result = await _householdQuery.ListMembersAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members/invite")]
    public async Task<IActionResult> Invite(Guid id, CancellationToken ct = default)
    {
        var invitedByUserId = User.GetUserId().Value;
        var result = await _manager.InviteAsync(
            new InviteHouseholdMemberCommand(id, invitedByUserId), ct);
        return CreatedAtAction(nameof(Get), new { id }, result);
    }

    [HttpPost("{id:guid}/members/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinHouseholdCommand request, CancellationToken ct = default)
    {
        var result = await _manager.JoinAsync(request, ct);
        return result is null
            ? NotFound()
            : CreatedAtAction(nameof(Get), new { id = result.HouseholdId }, result);
    }

    /// <summary>
    /// Join a household using only the invitation code — no householdId required.
    /// </summary>
    [HttpPost("/api/finance/members/join")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _manager.JoinByCodeAsync(request, userId, ct);
        return result is null
            ? NotFound(new { error = "Invalid invitation code." })
            : CreatedAtAction(nameof(Get), new { id = result.HouseholdId }, result);
    }

    [HttpDelete("{id:guid}/members/{membershipId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid membershipId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.RemoveAsync(new RemoveMembershipCommand(membershipId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPut("{id:guid}/members/{membershipId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(Guid id, Guid membershipId, [FromBody] ChangeMemberRoleBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await _manager.ChangeRoleAsync(new ChangeMembershipRoleCommand(membershipId, body.Role), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // ── User overview ─────────────────────────────────────────────────────────

    [HttpGet("/api/finance/overview")]
    public async Task<IActionResult> Overview(CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _householdQuery.GetUserOverviewAsync(userId, DateTime.UtcNow, ct);
        return Ok(result);
    }
}

public sealed record TransferOwnershipBody(Guid NewOwnerId);
public sealed record ChangeMemberRoleBody(HouseholdRole Role);
