namespace Finance.Domain.ValueObjects;

public sealed record MemberShare(AccountId MemberAccount, Money Amount);

// FundingAccount is an ordinary account — a member's own account or a shared pool; "funding"
// is the role it plays, not a distinct type. Any unallocated remainder (Total − Σ Shares) is
// borne by the funding account: their own share when it is a member, a no-op when it is a pool.
public sealed record ChargeAllocationContext(
    AccountId ExpenseAccount,
    AccountId FundingAccount,
    IReadOnlyList<MemberShare> Shares,
    Money Total,
    DateTime Date,
    string Description,
    string Source);

// Accrual basis: the cost is incurred and owed to the vendor but not yet funded, so there is no
// funding account. Any unallocated remainder (Total − Σ Shares) stays on the expense account.
public sealed record AccrualContext(
    AccountId ExpenseAccount,
    AccountId VendorPayableAccount,
    IReadOnlyList<MemberShare> Shares,
    Money Total,
    DateTime Date,
    string Description,
    string Source);

public sealed record TransferContext(
    AccountId DebitAccount,
    AccountId CreditAccount,
    Money Amount,
    DateTime ValueDate,
    string Description,
    string Source);

public sealed record JournalEntryDraft(
    DateTime Date,
    string Description,
    string? Source,
    IReadOnlyList<PostingLine> Lines);
