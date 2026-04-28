using Bills.Application.Contracts;
using Bills.Application.Managers;
using Bills.Application.Queries;
using Bills.Domain.ValueObjects;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/bills/households")]
public sealed class HouseholdsController : ControllerBase
{
    private readonly IHouseholdWorkflowManager _manager;
    private readonly IHouseholdMembershipManager _membershipManager;
    private readonly IHouseholdQuery _householdQuery;
    private readonly IHouseholdMembershipQuery _membershipQuery;
    private readonly IBillQuery _billQuery;
    private readonly IDashboardQuery _dashboardQuery;
    private readonly IBillSplitQuery _splitQuery;
    private readonly IIncomeQuery _incomeQuery;

    public HouseholdsController(
        IHouseholdWorkflowManager manager,
        IHouseholdMembershipManager membershipManager,
        IHouseholdQuery householdQuery,
        IHouseholdMembershipQuery membershipQuery,
        IBillQuery billQuery,
        IDashboardQuery dashboardQuery,
        IBillSplitQuery splitQuery,
        IIncomeQuery incomeQuery)
    {
        _manager = manager;
        _membershipManager = membershipManager;
        _householdQuery = householdQuery;
        _membershipQuery = membershipQuery;
        _billQuery = billQuery;
        _dashboardQuery = dashboardQuery;
        _splitQuery = splitQuery;
        _incomeQuery = incomeQuery;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _householdQuery.ListAsync(new ListHouseholdsRequest(userId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct = default)
    {
        var result = await _householdQuery.GetDetailAsync(new HouseholdDetailRequest(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Composite endpoint returning household + members + bills + dashboard in a single call.
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    public async Task<IActionResult> GetPage(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var household = await _householdQuery.GetDetailAsync(new HouseholdDetailRequest(id), ct);
        if (household is null) return NotFound();

        var members = await _membershipQuery.ListMembersAsync(id, ct);
        var bills = await _billQuery.ListAsync(new ListBillsRequest(id, 1, 500, true), ct);
        var dashboard = await _dashboardQuery.QueryAsync(new DashboardQueryRequest(id, periodStart, periodEnd), ct);

        // Replace the household-wide Net Balance with the current user's personal
        // net balance: their own income minus ALL their split obligations across
        // every household they belong to. This is the same figure shown on the
        // overview page so the two never disagree.
        var userIncome = await _incomeQuery.ListByUserAsync(
            new ListUserIncomeRequest(userId, 1, 500, true), ct);
        var userSplits = await _splitQuery.ListByUserWithBillDetailsAsync(
            UserId.Create(userId), periodStart, periodEnd, ct);

        var monthlyIncome = UserBudgetCalculator.MonthlyIncomeForUser(
            userIncome.Items, periodStart.Year, periodStart.Month);
        var monthlyObligations = UserBudgetCalculator.MonthlyObligationsForUser(
            userSplits, periodStart.Year, periodStart.Month);
        var userNetBalance = monthlyIncome - monthlyObligations;

        var correctedDashboard = dashboard with
        {
            TotalIncome = monthlyIncome,
            NetBalance = userNetBalance,
            IsOvercommitted = userNetBalance < 0,
        };

        return Ok(new HouseholdPageResponse(household, members, bills.Items, correctedDashboard));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHouseholdRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { OwnerId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { id = result.HouseholdId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHouseholdRequest request, CancellationToken ct = default)
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
            var result = await _manager.DeleteAsync(new DeleteHouseholdRequest(id, userId), ct);
            return result ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        try
        {
            var result = await _manager.TransferOwnershipAsync(new TransferHouseholdOwnershipRequest(id, body.NewOwnerId, userId), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

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

        var result = await _splitQuery.ListByHouseholdAsync(
            HouseholdId.Create(id), windowStart, windowEnd, ct);

        // Strip months with no contributions so the UI doesn't show empty cards
        var nonEmpty = result.Where(m => m.Members.Count > 0).ToList();
        return Ok(nonEmpty);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid id, CancellationToken ct = default)
    {
        var result = await _membershipQuery.ListMembersAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members/invite")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteHouseholdMemberRequest request, CancellationToken ct = default)
    {
        var result = await _membershipManager.InviteAsync(request with { HouseholdId = id }, ct);
        return CreatedAtAction(nameof(GetDetail), new { id }, result);
    }

    [HttpPost("{id:guid}/members/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinHouseholdRequest request, CancellationToken ct = default)
    {
        var result = await _membershipManager.JoinAsync(request, ct);
        return result is null
            ? NotFound()
            : CreatedAtAction(nameof(GetDetail), new { id = result.HouseholdId }, result);
    }

    [HttpDelete("{id:guid}/members/{membershipId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid membershipId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _membershipManager.RemoveAsync(new RemoveMembershipRequest(membershipId, userId.Value), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPut("{id:guid}/members/{membershipId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(Guid id, Guid membershipId, [FromBody] ChangeMemberRoleBody body, CancellationToken ct = default)
    {
        try
        {
            var result = await _membershipManager.ChangeRoleAsync(new ChangeMembershipRoleRequest(membershipId, body.Role), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

public sealed record TransferOwnershipBody(Guid NewOwnerId);
public sealed record ChangeMemberRoleBody(Bills.Domain.ValueObjects.HouseholdRole Role);

