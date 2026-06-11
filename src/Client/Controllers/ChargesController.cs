using Finance.Application.Commands;
using Finance.Application.Queries;
using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

/// <summary>
/// Charges — covers both personal (api/finance/expenses) and group-scoped
/// (api/finance/groups/{groupId}/expenses) routes. Both are driven by the same Charge aggregate.
/// Ledger postings are NOT coordinated here: every mutation commits its aggregate change and the
/// domain events it raised in one transaction (outbox), and the LedgerPostingConsumer brings the
/// books in step from those events — so a charge row can never be durably saved without its
/// ledger posting eventually following.
/// <para>
/// ROUTE NAMING: the URL segments <c>/expenses</c> and <c>/splits</c> are intentionally FROZEN for
/// API compatibility even though the domain renamed Expense→Charge and ExpenseSplit→Allocation. The
/// frontend (<c>frontend/lib/api/*</c>) calls these exact paths; renaming them is a breaking change
/// that must be a coordinated frontend+backend change, not a unilateral edit here.
/// </para>
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting("api")]
[Route("api/finance/expenses")]
public sealed class ChargesController : ControllerBase
{
    private readonly IChargeManager _manager;
    private readonly IChargeQuery _query;
    private readonly IBookkeepingManager _bookkeeping;

    public ChargesController(IChargeManager manager, IChargeQuery query, IBookkeepingManager bookkeeping)
    {
        _manager = manager;
        _query = query;
        _bookkeeping = bookkeeping;
    }

    // ── Personal expenses ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByUserAsync(new ListChargesParams(userId, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<IActionResult> GetDetail(Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetDetailAsync(new ChargeDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChargeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(GetDetail), new { expenseId = result.ChargeId }, result);
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<IActionResult> Update(Guid expenseId, [FromBody] UpdateChargeCommand request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { ChargeId = expenseId }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> Delete(Guid expenseId, CancellationToken ct = default)
    {
        var result = await _manager.DeleteAsync(new DeleteChargeCommand(expenseId), ct);
        return result is null ? NotFound() : NoContent();
    }

    [HttpPost("{expenseId:guid}/payments")]
    public async Task<IActionResult> MarkPaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkPaidAsync(new MarkChargePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}/payments")]
    public async Task<IActionResult> MarkUnpaid(Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkUnpaidAsync(new MarkChargeUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Group expenses ────────────────────────────────────────────────────

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses")]
    public async Task<IActionResult> ListByGroup(Guid groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.ListByGroupAsync(
            new ListGroupChargesParams(groupId, page, pageSize, ActiveOnly: true, CallerId: userId), ct);
        return Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> GetGroupDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.GetGroupDetailAsync(new GroupChargeDetailParams(expenseId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/detail")]
    public async Task<IActionResult> GetGroupFullDetail(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _query.GetGroupChargeDetailAsync(expenseId, userId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses")]
    public async Task<IActionResult> CreateGroup(Guid groupId, [FromBody] CreateGroupChargeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // The manager commits the charge + initial allocations and their ChargeCreated /
        // AllocationCreated events atomically (outbox); the LedgerPostingConsumer journals the
        // accrual and each share from those events.
        var result = await _manager.CreateGroupChargeAsync(
            request with { GroupId = groupId, CreatedBy = userId.Value }, ct);

        return CreatedAtAction(nameof(GetGroupDetail), new { groupId, expenseId = result.ChargeId }, result);
    }

    [HttpPut("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> UpdateGroup(Guid groupId, Guid expenseId, [FromBody] UpdateGroupChargeCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // ChargeUpdated drives the ledger re-sync — an amount/category/title edit reverses and
        // re-posts the accrual (and re-syncs the share postings) via the LedgerPostingConsumer.
        var result = await _manager.UpdateGroupChargeAsync(
            request with { ChargeId = expenseId, CallerId = userId.Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}")]
    public async Task<IActionResult> DeactivateGroup(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // ChargeDeactivated (committed with the soft-delete) drives the full unwind — accrual,
        // vendor payment and any settlements are reversed by the LedgerPostingConsumer.
        var result = await _manager.DeactivateGroupChargeAsync(
            new DeactivateChargeCommand(expenseId, userId.Value), ct);
        if (result is null) return NotFound();
        return NoContent();
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    public async Task<IActionResult> PayAllocation(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // The committed SettlementRecorded fact drives the ledger posting (Dr Cash / Cr Member)
        // via the LedgerPostingConsumer.
        await _manager.MarkPaidAsync(new MarkChargePaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/payments")]
    public async Task<IActionResult> UnpayAllocation(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // SettlementReversed drives the contra entry via the LedgerPostingConsumer.
        await _manager.MarkUnpaidAsync(new MarkChargeUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Vendor payment (the bill itself) ─────────────────────────────────────────────

    /// <summary>Mark the vendor paid for an occurrence of a group charge, choosing who paid now:
    /// a member fronted it (FundingSource=PayerMember) or it came from the shared pot (GroupCash).
    /// Posts Dr Vendor Payable / Cr funding. "Is it paid" is then derived from the ledger.</summary>
    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/vendor-payment")]
    public async Task<IActionResult> PayVendor(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // The committed VendorPaid fact drives the ledger transfer (Dr Vendor Payable / Cr funding)
        // via the LedgerPostingConsumer.
        await _manager.MarkVendorPaidAsync(
            new MarkVendorPaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    /// <summary>Undo a vendor payment — VendorPaymentReversed drives the contra entry.</summary>
    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/vendor-payment")]
    public async Task<IActionResult> UnpayVendor(Guid groupId, Guid expenseId, [FromBody] PaymentOccurrenceBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        await _manager.MarkVendorUnpaidAsync(new MarkVendorUnpaidCommand(expenseId, userId.Value, body.OccurrenceDate), ct);
        return NoContent();
    }

    // ── Settle-up (member-to-member) ─────────────────────────────────────────────────

    /// <summary>The caller records a direct settle-up payment to another member (squaring what they
    /// owe). Self-service — the caller is always the payer (from). Posts Dr Member:to / Cr Member:from.</summary>
    [HttpPost("/api/finance/groups/{groupId:guid}/settlements/transfer")]
    public async Task<IActionResult> SettleUpTransfer(Guid groupId, [FromBody] SettleUpTransferBody body, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (body.ToUserId == userId.Value) return BadRequest(new { error = "Can't settle up with yourself." });
        if (body.Amount <= 0) return BadRequest(new { error = "Amount must be positive." });

        // Unique source per payment (settle-ups repeat over time, so they are not idempotent on a
        // fixed key); the random suffix keeps a double-submit from being silently swallowed mid-pay.
        var source = $"settleup:{groupId:N}:{userId.Value:N}:{body.ToUserId:N}:{Guid.NewGuid():N}";
        await _bookkeeping.RecordMemberTransferAsync(groupId, userId.Value, body.ToUserId, body.Amount, body.Currency, source, ct);
        return NoContent();
    }

    // ── Allocations ────────────────────────────────────────────────────────────────

    [HttpGet("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    public async Task<IActionResult> ListAllocations(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var result = await _query.ListAllocationsAsync(new ListAllocationsParams(expenseId), ct);
        return Ok(result);
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits")]
    public async Task<IActionResult> UpsertAllocation(Guid groupId, Guid expenseId, [FromBody] UpsertAllocationCommand request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();

        // Self-service only: this endpoint always attributes the share to the authenticated caller.
        // Assigning ANOTHER member's share is role-gated and goes through the household service
        // (POST /api/households/{groupId}/charges/{expenseId}/allocations), which checks Owner/Admin
        // then emits GroupAllocationAssigned for finance to apply. Reject an explicit foreign userId
        // rather than silently re-attributing it to the caller.
        if (request.UserId != Guid.Empty && request.UserId != userId.Value)
            return Forbid();

        try
        {
            // AllocationCreated/AllocationUpdated (committed with the upsert) drive the share's
            // ledger posting — reverse-then-repost on re-amounting — via the LedgerPostingConsumer.
            var written = await _manager.UpsertAllocationAsync(
                request with { ChargeId = expenseId, GroupId = groupId, UserId = userId.Value }, ct);

            // Re-read via the query layer so the caller gets the same enriched shape
            // (displayName, avatarUrl, membershipRole, occurrence-aware isPaid) that the
            // detail GET returns — keeps frontend schemas single-shape per logical entity.
            var enriched = await _query.GetAllocationDetailAsync(written.AllocationId, ct);
            return enriched is null ? Ok(written) : Ok(enriched);
        }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/even")]
    public async Task<IActionResult> AllocateEvenly(Guid groupId, Guid expenseId, [FromBody] AllocateEvenlyBody body, CancellationToken ct = default)
    {
        // Each touched share commits an AllocationCreated/AllocationUpdated event; the
        // LedgerPostingConsumer re-journals every one (reverse then post, since amounts change).
        await _manager.AllocateEvenlyAsync(expenseId, body.UserIds, ct);
        return NoContent();
    }

    [HttpDelete("/api/finance/groups/{groupId:guid}/expenses/{expenseId:guid}/splits/{splitId:guid}")]
    public async Task<IActionResult> RemoveAllocation(Guid groupId, Guid expenseId, Guid splitId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        // AllocationRemoved drives the reversal of the share's posting via the LedgerPostingConsumer.
        var result = await _manager.RemoveAllocationAsync(new RemoveAllocationCommand(splitId, userId.Value), ct);
        if (result is null) return NotFound();
        return NoContent();
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns per-month, per-member contribution breakdowns for a household.
    /// Window: 3 past months + current month + 8 future months (12 total).
    /// </summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/contributions")]
    public async Task<IActionResult> GetContributions(Guid groupId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var windowEnd = windowStart.AddMonths(12).AddDays(-1);
        var result = await _query.ListAllocationsByGroupAsync(
            GroupId.Create(groupId), windowStart, windowEnd, ct);
        var nonEmpty = result.Where(m => m.Members.Count > 0).ToList();
        return Ok(nonEmpty);
    }

    /// <summary>
    /// Per-member balance ("YOU OWE / YOU'RE OWED") for the caller within a group.
    /// </summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/balances")]
    public async Task<IActionResult> GetMemberBalances(Guid groupId, CancellationToken ct = default)
    {
        var callerId = User.GetUserId().Value;
        var result = await _query.ListMemberBalancesAsync(GroupId.Create(groupId), callerId, ct);
        return Ok(result);
    }

    /// <summary>Most recent fully-settled period or null if none.</summary>
    [HttpGet("/api/finance/groups/{groupId:guid}/last-settlement")]
    public async Task<IActionResult> GetLastSettlement(Guid groupId, CancellationToken ct = default)
    {
        var result = await _query.GetLastSettlementAsync(GroupId.Create(groupId), ct);
        return result is null ? NoContent() : Ok(result);
    }
}
