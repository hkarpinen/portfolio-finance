using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public enum ChargeScope
{
    /// <summary>An expense owned by a single user.</summary>
    Personal,
    /// <summary>An expense owned by a household (referenced by opaque GroupId per ARCHITECTURE-boundaries.md).</summary>
    Group
}

/// <summary>
/// Unified expense response shape. Replaces the former pair `ChargeDto` (personal) and
/// `GroupChargeDto` (shared). Fields specific to one scope are nullable; <see cref="Scope"/>
/// is the discriminator.
/// </summary>
public sealed record ChargeResponseDto(
    Guid ChargeId,
    ChargeScope Scope,
    Guid? UserId,                   // set when Scope == Personal
    Guid? GroupId,                  // set when Scope == Group
    Guid? CreatedBy,                // creator user-id; set when Scope == Group
    string Title,
    string? Description,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency,
    DateTime? RecurrenceStartDate,
    DateTime? RecurrenceEndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsPaid = false,            // Personal: paid status. Group: caller's *share* pay status (CallerIsPaid).
    DateTime? CurrentOccurrenceDate = null,
    Guid? PayerUserId = null,
    FundingSource? FundingSource = null,    // set when Scope == Group; null for personal
    // Group only: has the VENDOR been paid (the bill itself), distinct from the caller's share
    // (IsPaid). Derived from the ledger (Vendor Payable balance); PayerUserId/FundingSource describe
    // who paid / how. Legacy cash-basis charges read as vendor-paid.
    bool VendorPaid = false);

public sealed record ChargeListDto(IReadOnlyCollection<ChargeResponseDto> Items, int TotalCount);

/// <summary>Renamed from GroupChargeListDto per ARCHITECTURE-boundaries.md (Group→Group).</summary>
public sealed record GroupChargeListDto(IReadOnlyCollection<ChargeResponseDto> Items, int TotalCount);

/// <summary>Allocation enriched with member display name.</summary>
public sealed record AllocationDetailDto(
    Guid AllocationId,
    Guid UserId,
    string? DisplayName,
    string? AvatarUrl,
    string MembershipRole,
    decimal Amount,
    string Currency,
    bool IsPaid);

/// <summary>Composite read model — group expense with enriched splits. Renamed from GroupChargeDetailDto.</summary>
public sealed record GroupChargeDetailDto(
    ChargeResponseDto Charge,
    IReadOnlyCollection<AllocationDetailDto> Allocations);

public sealed record AllocationDto(
    Guid AllocationId,
    Guid ChargeId,
    Guid GroupId,
    Guid UserId,
    decimal Amount,
    string Currency,
    bool IsPaid,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CallerAllocationStatusDto(Guid AllocationId, bool IsPaid);
