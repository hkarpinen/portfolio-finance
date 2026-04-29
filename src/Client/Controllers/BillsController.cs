using Finance.Application.Contracts;
using Finance.Application.Managers;
using Finance.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/households/{householdId:guid}/bills")]
public sealed class BillsController : ControllerBase
{
    private readonly IBillWorkflowManager _manager;
    private readonly IBillQuery _billQuery;
    private readonly IHouseholdMembershipQuery _membershipQuery;

    public BillsController(
        IBillWorkflowManager manager,
        IBillQuery billQuery,
        IHouseholdMembershipQuery membershipQuery)
    {
        _manager = manager;
        _billQuery = billQuery;
        _membershipQuery = membershipQuery;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid householdId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _billQuery.ListAsync(new ListBillsRequest(householdId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{billId:guid}")]
    public async Task<IActionResult> GetDetail(Guid householdId, Guid billId, CancellationToken ct = default)
    {
        var result = await _billQuery.GetDetailAsync(new BillDetailRequest(billId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Composite endpoint returning bill + enriched splits + members + caller's role in a single call.
    /// </summary>
    [HttpGet("{billId:guid}/detail")]
    public async Task<IActionResult> GetBillPage(Guid householdId, Guid billId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;

        var bill = await _billQuery.GetDetailAsync(new BillDetailRequest(billId), ct);
        if (bill is null) return NotFound();

        var splits = await _billQuery.ListSplitsAsync(new ListSplitsRequest(billId), ct);
        var members = await _membershipQuery.ListMembersAsync(householdId, ct);

        var memberDict = members.ToDictionary(m => m.MembershipId);
        var currentUserRole = members.FirstOrDefault(m => m.UserId == userId)?.Role.ToString();

        var enrichedSplits = splits.Select(s =>
        {
            memberDict.TryGetValue(s.MembershipId, out var member);
            return new SplitDetailResponse(
                s.SplitId,
                s.MembershipId,
                s.UserId,
                member?.DisplayName,
                null, // AvatarUrl not stored on membership
                member?.Role.ToString() ?? "Member",
                s.Amount,
                s.Currency,
                s.IsClaimed,
                s.ClaimedAt,
                s.ClaimedBy);
        }).ToList();

        return Ok(new BillPageResponse(bill, enrichedSplits, members, currentUserRole));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid householdId, [FromBody] CreateBillRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { HouseholdId = householdId, CreatedBy = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { householdId, billId = result.BillId }, result);
    }

    [HttpPut("{billId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid billId, [FromBody] UpdateBillRequest request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { BillId = billId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{billId:guid}")]
    public async Task<IActionResult> Deactivate(Guid householdId, Guid billId, CancellationToken ct = default)
    {
        var result = await _manager.DeactivateAsync(new DeactivateBillRequest(billId), ct);
        return result is null ? NotFound() : NoContent();
    }
}

