using Finance.Application.Dtos;
using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Client.Extensions;
using Client.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

/// <summary>
/// Personal and group-scoped expense routes, both over the same aggregate.
///
/// No ledger posting is coordinated here. Each mutation commits its aggregate change
/// and the events it raised in one transaction, and the books follow from those events
/// — so the response returns BEFORE the ledger moves.
///
/// The <c>/expenses</c> and <c>/splits</c> URL segments are frozen for API compatibility
/// despite the domain rename to Expense and Share. Changing them is a breaking change.
/// </summary>
[ApiController]
[Authorize]
// Group routes on this controller are members-only. A no-op on the personal
// routes above, which carry no {groupId}.
[RequireGroupMembership]
[EnableRateLimiting("api")]
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
    [ProducesResponseType(typeof(ExpenseListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByUserAsync(new ListExpensesParams(userId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{expenseId:guid}")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.GetDetailAsync(new ExpenseDetailParams(expenseId, userId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { CallerUserId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { expenseId = result.ExpenseId }, result);
    }

    [HttpPut("{expenseId:guid}")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid expenseId, [FromBody] UpdateExpenseCommand request, CancellationToken ct = default)
    {
        // From the token, OVERWRITING the body — it is bindable, so a client could
        // otherwise nominate whichever owner makes the check pass.
        var userId = User.GetUserId().Value;
        var result = await _manager.UpdateAsync(request with { ExpenseId = expenseId, CallerUserId = userId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{expenseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _manager.DeleteAsync(new DeleteExpenseCommand(expenseId, userId), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPost("{expenseId:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkPaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkExpensePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkUnpaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkExpenseUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses")]
    [ProducesResponseType(typeof(GroupExpenseListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByGroup(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByGroupAsync(
            new ListGroupExpensesParams(groupId, page, pageSize, ActiveOnly: true, CallerUserId: userId), ct);
        return Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetGroupDetailAsync(new GroupExpenseDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/detail")]
    [ProducesResponseType(typeof(GroupExpenseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupFullDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.GetGroupExpenseDetailAsync(expenseId, userId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup(Guid groupId, [FromBody] CreateGroupExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateGroupExpenseAsync(
            request with { GroupId = groupId, CallerUserId = userId.Value }, ct);

        return CreatedAtAction(nameof(GetGroupDetail), new { groupId, expenseId = result.ExpenseId }, result);
    }

    [HttpPut("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    [ProducesResponseType(typeof(ExpenseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateGroup(Guid groupId, Guid expenseId, [FromBody] UpdateGroupExpenseCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.UpdateGroupExpenseAsync(
            request with { ExpenseId = expenseId, CallerUserId = userId.Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateGroup(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.DeactivateGroupExpenseAsync(
            new DeactivateExpenseCommand(expenseId, userId.Value), ct);
        if (result is null) return NotFound();
        return NoContent();
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PayShare(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkExpensePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnpayShare(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkExpenseUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    /// <summary>Mark the vendor paid for an occurrence of a group expense, choosing who paid now:
    /// a member fronted it (FundingSource=PayerMember) or it came from the shared pot (GroupCash).
    /// Posts Dr Vendor Payable / Cr funding. "Is it paid" is then derived from the ledger.</summary>
    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/vendor-payment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PayVendor(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // The committed VendorPaid fact drives the ledger transfer (Dr Vendor Payable / Cr funding); it
        // is not posted here.
        await _manager.MarkVendorPaidAsync(
            new MarkVendorPaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    /// <summary>Undo a vendor payment — VendorPaymentReversed drives the contra entry.</summary>
    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/vendor-payment")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnpayVendor(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkVendorUnpaidAsync(new MarkVendorUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    /// <summary>The caller records a direct settle-up payment to another member (squaring what they
    /// owe). Self-service — the caller is always the payer (from). Posts Dr Member:to / Cr Member:from.</summary>
    [HttpPost("/api/finance/groups/{groupId:guid}/settlements/transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SettleUpTransfer(Guid groupId, [FromBody] SettleUpTransferBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // Answered as a 400 rather than letting the document's refusal surface as a 500. The rules
        // themselves are on MemberTransfer, so anything arriving another way meets them too.
        if (body.ToUserId == userId.Value) return BadRequest(new { error = "Can't settle up with yourself." });
        if (body.Amount <= 0) return BadRequest(new { error = "Amount must be positive." });

        await _manager.SettleUpAsync(
            groupId: groupId,
            fromUserId: userId.Value,
            toUserId: body.ToUserId,
            amount: body.Amount,
            currency: body.Currency,
            ct: ct);
        return NoContent();
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ShareDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListShares(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.ListSharesAsync(new ListSharesParams(expenseId), ct);
        return Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertShare(Guid groupId, Guid expenseId, [FromBody] UpsertShareCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();

        // Self-service only: this endpoint always attributes the share to the authenticated caller.
        // Assigning ANOTHER member's share is role-gated and arrives asynchronously as
        // GroupShareAssigned. An explicit foreign userId is rejected rather than silently
        // re-attributed to the caller.
        if (request.MemberUserId != Guid.Empty && request.MemberUserId != userId.Value)
            return Forbid();

        try
        {
            // ShareCreated/ShareUpdated, committed with the upsert, drive the share's ledger
            // posting — reverse-then-repost on re-amounting.
            var written = await _manager.UpsertShareAsync(
                request with { ExpenseId = expenseId, GroupId = groupId, MemberUserId = userId.Value, CallerUserId = userId.Value }, ct);

            // Re-read via the query layer so the caller gets the same enriched shape
            // (displayName, avatarUrl, membershipRole, occurrence-aware isPaid) that the
            // detail GET returns — keeps frontend schemas single-shape per logical entity.
            var enriched = await _query.GetShareDetailAsync(written.ShareId, ct);
            return enriched is null ? Ok(written) : Ok(enriched);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/even")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SplitEvenly(Guid groupId, Guid expenseId, [FromBody] SplitEvenlyBody body, CancellationToken ct = default)
    {
        // Each touched share commits a ShareCreated/ShareUpdated event; the
        // LedgerJournalConsumer re-journals every one (reverse then post, since amounts change).
        await _manager.SplitEvenlyAsync(expenseId, body.UserIds, ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/{shareId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveShare(Guid groupId, Guid expenseId, Guid shareId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // ShareRemoved drives the reversal of the share's posting via the LedgerJournalConsumer.
        var removed = await _manager.RemoveShareAsync(new RemoveShareCommand(shareId, userId.Value), ct);
        if (removed is null) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Returns per-month, per-member contribution breakdowns for a group.
    /// Window: 3 past months + current month + 8 future months (12 total).
    /// </summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/contributions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<GroupMonthlyContributionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContributions(Guid groupId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var windowEnd = windowStart.AddMonths(12).AddDays(-1);
        var result = await _query.ListSharesByGroupAsync(
            GroupId.Create(groupId), windowStart, windowEnd, ct);
        var nonEmpty = result.Where(m => m.Members.Count > 0).ToList();
        return Ok(nonEmpty);
    }

    /// <summary>
    /// Per-member balance ("YOU OWE / YOU'RE OWED") for the caller within a group.
    /// </summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/balances")]
    [ProducesResponseType(typeof(MemberBalanceListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemberBalances(Guid groupId, CancellationToken ct = default)
    {
        var callerUserId = User.GetUserId().Value;
        var result = await _query.ListMemberBalancesAsync(GroupId.Create(groupId), callerUserId, ct);
        return Ok(result);
    }

    /// <summary>Most recent fully-settled period or null if none.</summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/last-settlement")]
    [ProducesResponseType(typeof(SettlementSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetLastSettlement(Guid groupId, CancellationToken ct = default)
    {
        var result = await _query.GetLastSettlementAsync(GroupId.Create(groupId), ct);
        return result is null ? NoContent() : Ok(result);
    }
}
