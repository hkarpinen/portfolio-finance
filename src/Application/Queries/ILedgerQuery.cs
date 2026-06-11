namespace Finance.Application.Queries;

/// <summary>One account with its derived balance (oriented to its normal balance).</summary>
public sealed record AccountBalanceDto(
    Guid AccountId,
    string Code,
    string Name,
    string AccountType,
    string NormalBalance,
    decimal Balance);

/// <summary>
/// A ledger's read view — its chart of accounts with derived balances, plus the trial
/// balance (P5). <see cref="IsBalanced"/> is the health check: total debits == total credits.
/// </summary>
public sealed record LedgerViewDto(
    Guid LedgerId,
    string Currency,
    IReadOnlyList<AccountBalanceDto> Accounts,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced);

/// <summary>One posting against an account, with the running balance after it (oriented to the
/// account's normal balance). <see cref="Source"/> is the originating document; <see cref="IsReversal"/>
/// marks a correcting (reversing) entry.</summary>
public sealed record StatementLineDto(
    Guid EntryId,
    DateTime Date,
    string Description,
    string? Source,
    string Direction,
    decimal Amount,
    decimal RunningBalance,
    bool IsReversal);

/// <summary>An account's full posting history (statement), oldest first, with a running balance.
/// <see cref="Balance"/> is the closing balance (== the last line's running balance).</summary>
public sealed record AccountStatementDto(
    Guid AccountId,
    string Code,
    string Name,
    string AccountType,
    string NormalBalance,
    string Currency,
    decimal Balance,
    IReadOnlyList<StatementLineDto> Lines);

/// <summary>The caller's net position in one group — their <c>Member:{userId}</c> equity balance
/// (credit-normal): positive = the group owes them / they've overpaid, negative = they owe.</summary>
public sealed record GroupPositionDto(Guid GroupId, string Currency, decimal Net);

/// <summary>A user's financial position across ALL their groups, derived from the group ledgers'
/// member-equity accounts — no separate user ledger. <see cref="Total"/> sums the per-group nets.</summary>
public sealed record UserPositionDto(decimal Total, string? Currency, IReadOnlyList<GroupPositionDto> Groups);

public interface ILedgerQuery
{
    /// <summary>The group's ledger view, or null if no ledger has been opened for the group yet.</summary>
    Task<LedgerViewDto?> GetGroupLedgerAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>One account's statement (its postings + running balance), or null if the group's
    /// ledger or the account doesn't exist.</summary>
    Task<AccountStatementDto?> GetAccountStatementAsync(Guid groupId, Guid accountId, CancellationToken ct = default);

    /// <summary>The caller's net position across every group they have a member account in, derived
    /// from the group ledgers (the reciprocal of a personal "due to/from group" — recorded once, not
    /// double-posted). Groups where they have no postings are omitted (net 0).</summary>
    Task<UserPositionDto> GetUserPositionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>True if an active settlement covers this allocation's occurrence in the ledger (the
    /// single source of truth). Lets the charge context keep settle/un-settle idempotent without
    /// holding settled-state of its own.</summary>
    Task<bool> IsAllocationSettledAsync(Guid allocationId, DateTime occurrence, CancellationToken ct = default);

    /// <summary>True when the vendor has been paid for this charge — derived from the Vendor Payable
    /// balance (owed == 0). Member settlement is gated on this: a share can only be settled once the
    /// bill itself is funded. Legacy cash-basis charges (which never touched Vendor Payable) read paid.</summary>
    Task<bool> IsVendorPaidAsync(Guid chargeId, CancellationToken ct = default);
}
