namespace Finance.Domain.ValueObjects;

/// <summary>One member's share of a cost, as the books see it.</summary>
public sealed record MemberShare(AccountId MemberAccount, Money Amount);
