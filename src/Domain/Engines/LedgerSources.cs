namespace Finance.Domain.Engines;

/// <summary>
/// How a journal entry names what it came from. Deterministic, so a later reversal can find the
/// entry it is reversing, and so the partial unique index on `(ledger_id, source)` can forbid a
/// second active entry under one origin.
///
/// A sibling of <see cref="ChartCodes"/>: that one names accounts, this one names entry origins.
/// Every key lives here, including the two the aggregates expose as <c>LedgerSource</c> — two
/// spellings of one key is how the reversal lookup silently stops matching.
/// </summary>
public static class LedgerSources
{
    public static string Expense(Guid expenseId) => $"expense:{expenseId:N}";

    public static string Settlement(Guid expenseId, DateTime occurrence, Guid fromUserId)
        => $"settlement:{expenseId:N}:{occurrence:yyyyMMdd}:{fromUserId:N}";

    public static string VendorPayment(Guid expenseId, DateTime occurrence)
        => $"vendorpayment:{expenseId:N}:{occurrence:yyyyMMdd}";

    /// <summary>Per-share source so a member's share is journaled (and reversible) on its own,
    /// whether it was added at creation or later — Dr Member / Cr Expense under this key.</summary>
    public static string Share(Guid shareId) => $"share:{shareId:N}";

    public static string SettleUp(Guid transferId) => $"settleup:{transferId:N}";

    public static string Receipt(Guid receiptId) => $"receipt:{receiptId:N}";
}
