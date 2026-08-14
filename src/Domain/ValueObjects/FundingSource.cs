namespace Finance.Domain.ValueObjects;

// Which account funds the vendor payment for a group expense. The journal structure is identical
// either way — only the account credited as the funding account differs — so collect-first and
// reimburse-after net to the same journal_lines.
public enum FundingSource
{
    // Front-and-reimburse: FundingAccount = Member:{payer}. The payer's own share is intrinsically
    // covered by fronting it, so there is nothing for them to settle; other members repay the payer.
    PayerMember = 0,

    // Pooled / collect-first: FundingAccount = Cash. Every member — including whoever physically paid
    // — owes their share into the pool and settles it reversibly, so every share (the payer's
    // included) must be allocated explicitly.
    GroupCash = 1,
}
