using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateChargeCommand(
    Guid UserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

// CallerId is the owner check: the charge id alone must never authorise an update or a delete.
public sealed record UpdateChargeCommand(
    Guid ChargeId,
    Guid CallerId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

// CallerId is the owner check.
public sealed record DeleteChargeCommand(Guid ChargeId, Guid CallerId);

public sealed record CreateAllocationDto(Guid UserId, decimal Amount, string Currency);

public sealed record CreateGroupChargeCommand(
    Guid GroupId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    Guid CreatedBy,
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
    Guid CallerId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null,
    Guid? PayerUserId = null);

public sealed record DeactivateChargeCommand(Guid ChargeId, Guid CallerId);

public sealed record UpsertAllocationCommand(
    Guid? AllocationId,
    Guid ChargeId,
    Guid GroupId,
    Guid UserId,
    decimal Amount,
    string Currency);

public sealed record RemoveAllocationCommand(Guid AllocationId, Guid CallerId);

public sealed record MarkChargePaidCommand(
    Guid ChargeId,
    Guid UserId,
    DateTime OccurrenceDate,
    string? TransactionReference = null);

public sealed record MarkChargeUnpaidCommand(
    Guid ChargeId,
    Guid UserId,
    DateTime OccurrenceDate);

// Owner-only: the bill's owner pays the vendor from the shared pot (collect-first).
public sealed record MarkVendorPaidCommand(
    Guid ChargeId,
    Guid CallerId,
    DateTime OccurrenceDate);

public sealed record MarkVendorUnpaidCommand(
    Guid ChargeId,
    Guid CallerId,
    DateTime OccurrenceDate);

public sealed record PaymentOccurrenceBody(DateTime OccurrenceDate);
public sealed record SettleUpTransferBody(Guid ToUserId, decimal Amount, string Currency);
public sealed record AllocateEvenlyBody(IReadOnlyList<Guid> UserIds);
