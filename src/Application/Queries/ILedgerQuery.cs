namespace Finance.Application.Queries;

// Balance is oriented to the account's normal balance.
public sealed record AccountBalanceDto(
    Guid AccountId,
    string Code,
    string Name,
    string AccountType,
    string NormalBalance,
    decimal Balance);

public sealed record LedgerViewDto(
    Guid LedgerId,
    string Currency,
    IReadOnlyList<AccountBalanceDto> Accounts,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced);

// RunningBalance is oriented to the account's normal balance.
public sealed record StatementLineDto(
    Guid EntryId,
    DateTime Date,
    string Description,
    string? Source,
    string Direction,
    decimal Amount,
    decimal RunningBalance,
    bool IsReversal);

// Lines are oldest first; Balance is the closing balance (== the last line's running balance).
public sealed record AccountStatementDto(
    Guid AccountId,
    string Code,
    string Name,
    string AccountType,
    string NormalBalance,
    string Currency,
    decimal Balance,
    IReadOnlyList<StatementLineDto> Lines);

// Credit-normal: positive = the group owes them / they have overpaid, negative = they owe.
public sealed record GroupPositionDto(Guid GroupId, string Currency, decimal Net);

// Derived from the group ledgers' member-equity accounts — there is no separate user ledger.
public sealed record UserPositionDto(decimal Total, string? Currency, IReadOnlyList<GroupPositionDto> Groups);

public interface ILedgerQuery
{
    Task<LedgerViewDto?> GetGroupLedgerAsync(Guid groupId, CancellationToken ct = default);

    Task<AccountStatementDto?> GetAccountStatementAsync(Guid groupId, Guid accountId, CancellationToken ct = default);

    // Groups where they have no journal lines are omitted rather than reported as net 0.
    Task<UserPositionDto> GetUserPositionAsync(Guid userId, CancellationToken ct = default);

    // The ledger is the single source of truth for settlement, which lets the expense context keep
    // settle/un-settle idempotent without holding settled-state of its own.
    Task<bool> IsShareSettledAsync(Guid shareId, DateTime occurrence, CancellationToken ct = default);

    // Derived from the Vendor Payable balance (owed == 0). Member settlement is gated on this: a
    // share can only be settled once the expense itself is funded. Legacy cash-basis expenses never
    // touched Vendor Payable, so they read as paid.
    Task<bool> IsVendorPaidAsync(Guid expenseId, CancellationToken ct = default);
}
