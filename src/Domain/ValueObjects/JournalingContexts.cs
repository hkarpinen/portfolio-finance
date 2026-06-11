namespace Finance.Domain.ValueObjects;

/// <summary>One member's allocated share of an expense, against their (already-resolved) account.</summary>
public sealed record MemberShare(AccountId MemberAccount, Money Amount);

/// <summary>
/// Everything the journalizing engine needs to post a charge — with account ids already
/// resolved by the caller (the engine performs no I/O). <see cref="FundingAccount"/> is just
/// an ordinary account reference (an <c>AccountId</c>) — the one this charge was paid from:
/// a member's own account (the "one payer" case) or a shared pool/card (a future source).
/// "Funding" is the role it plays here, not a type or a distinct object; the engine treats
/// both identically. <see cref="Shares"/> are the explicit member allocations; any unallocated
/// remainder (Total − Σ shares) is borne by the funding account, which is correct when it is
/// itself a member (their own share) and a no-op (remainder == 0) when it is a pool.
/// </summary>
public sealed record ChargeAllocationContext(
    AccountId ExpenseAccount,
    AccountId FundingAccount,
    IReadOnlyList<MemberShare> Shares,
    Money Total,
    DateTime Date,
    string Description,
    string Source);

/// <summary>
/// Everything the engine needs to post a charge on an ACCRUAL basis — the cost is incurred and
/// owed to the vendor, but not yet funded. <see cref="VendorPayableAccount"/> carries the
/// liability (we owe the vendor); funding is recorded later by a separate transfer when the bill
/// is actually paid. Unlike <see cref="ChargeAllocationContext"/> there is no funding account:
/// any unallocated remainder (Total − Σ shares) stays on the Expense account (household-borne).
/// </summary>
public sealed record AccrualContext(
    AccountId ExpenseAccount,
    AccountId VendorPayableAccount,
    IReadOnlyList<MemberShare> Shares,
    Money Total,
    DateTime Date,
    string Description,
    string Source);

/// <summary>
/// A balanced move of <see cref="Amount"/> between two accounts — the engine's primitive for
/// every 1↔1 event (settlement, contribution, payment, payoff). Both fields are just accounts:
/// the entry debits <see cref="DebitAccount"/> and credits <see cref="CreditAccount"/>. The
/// business roles (which is the member, which is the funding account) are resolved by the caller
/// and are deliberately absent here — at the posting level there are only accounts and a direction.
/// </summary>
public sealed record TransferContext(
    AccountId DebitAccount,
    AccountId CreditAccount,
    Money Amount,
    DateTime ValueDate,
    string Description,
    string Source);

/// <summary>
/// The engine's output: a not-yet-posted journal entry. The caller turns each draft into
/// a <c>JournalEntry</c> via the domain factory (which re-validates the balance).
/// </summary>
public sealed record JournalEntryDraft(
    DateTime Date,
    string Description,
    string? Source,
    IReadOnlyList<PostingLine> Lines);
