using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateChargeCommand(
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null,
    /// <summary>Which of the caller's accounts settles it — a card, or checking. Null means cash,
    /// which is the honest default for somebody who has told us about no accounts.</summary>
    Guid? FundingAccountId = null);

// CallerUserId is WHO IS ASKING, on every command that changes something. A charge id on its own
// must never authorise an update, a delete or a re-split — the aggregate decides, via
// Charge.IsManagedBy.
//
// Anyone the operation is ABOUT keeps a role name instead: MemberUserId, PayerUserId. The two were
// both called UserId, which meant the same word was the actor on one command and the subject on
// the next, and nothing but the manager could tell you which.
public sealed record UpdateChargeCommand(
    Guid ChargeId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    // No cadence here. Amending a bill amends that one bill; how often it comes round is the
    // schedule's business, and a copy on the bill is what let the two disagree.
    string? Description = null);

public sealed record DeleteChargeCommand(Guid ChargeId, Guid CallerUserId);

/// <summary>Whose share it is — an actor never appears in a nested DTO.</summary>
public sealed record CreateAllocationDto(Guid MemberUserId, decimal Amount, string Currency);

public sealed record CreateGroupChargeCommand(
    Guid GroupId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    Guid CallerUserId,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null,
    Guid? PayerUserId = null,
    FundingSource FundingSource = FundingSource.PayerMember,
    IReadOnlyList<CreateAllocationDto>? Allocations = null);

public sealed record UpdateGroupChargeCommand(
    Guid ChargeId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    DateTime DueDate,
    string? Description = null,
    Guid? PayerUserId = null);

public sealed record DeactivateChargeCommand(Guid ChargeId, Guid CallerUserId);

public sealed record UpsertAllocationCommand(
    Guid? AllocationId,
    Guid ChargeId,
    Guid GroupId,
    Guid CallerUserId,
    /// <summary>Whose share this is — not who is assigning it.</summary>
    Guid MemberUserId,
    decimal Amount,
    string Currency);

public sealed record RemoveAllocationCommand(Guid AllocationId, Guid CallerUserId);

public sealed record MarkChargePaidCommand(
    Guid ChargeId,
    Guid CallerUserId,
    DateTime OccurrenceDate,
    string? TransactionReference = null,
    /// <summary>Which account the money came from. Null keeps whatever the charge already named,
    /// and failing that, cash.</summary>
    Guid? FundingAccountId = null);

public sealed record MarkChargeUnpaidCommand(
    Guid ChargeId,
    Guid CallerUserId,
    DateTime OccurrenceDate);

// Owner-only: the bill's owner pays the vendor from the shared pot (collect-first).
public sealed record MarkVendorPaidCommand(
    Guid ChargeId,
    Guid CallerUserId,
    DateTime OccurrenceDate);

public sealed record MarkVendorUnpaidCommand(
    Guid ChargeId,
    Guid CallerUserId,
    DateTime OccurrenceDate);

public sealed record PaymentOccurrenceBody(DateTime OccurrenceDate);
public sealed record SettleUpTransferBody(Guid ToUserId, decimal Amount, string Currency);
public sealed record AllocateEvenlyBody(IReadOnlyList<Guid> UserIds);
