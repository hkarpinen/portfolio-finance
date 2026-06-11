namespace Finance.Domain.ValueObjects;

/// <summary>
/// Which account funds the vendor payment for a group charge — the single volatility axis of the
/// ledger (ledger-design.md §11.1). The journal structure is identical for both cases; only which
/// account is credited as the funding account differs, so <c>collect-first</c> and
/// <c>reimburse-after</c> net to the same postings.
/// </summary>
public enum FundingSource
{
    /// <summary>
    /// Front-and-reimburse: the payer fronts the whole bill from their own pocket
    /// (FundingAccount = <c>Member:{payer}</c>). The payer's own share is intrinsically covered by
    /// fronting it (nothing to settle); other members repay the payer. The historical default.
    /// </summary>
    PayerMember = 0,

    /// <summary>
    /// Pooled / collect-first: the vendor is paid from the group's shared <c>Cash</c>
    /// (FundingAccount = Cash). Every member — including whoever physically paid — owes their share
    /// into the pool and settles it reversibly. Requires each member's share (the payer's included)
    /// to be allocated explicitly.
    /// </summary>
    GroupCash = 1,
}
