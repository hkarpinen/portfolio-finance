namespace Finance.Domain.ValueObjects;

/// <summary>Internal identifier for a user's linked financial institution connection.</summary>
public readonly record struct FinancialConnectionId(Guid Value)
{
    public static FinancialConnectionId New() => new(Guid.NewGuid());
    public static FinancialConnectionId Create(Guid value) => new(value);
}

public enum FinancialConnectionStatus
{
    Healthy = 0,
    /// <summary>User must re-authenticate via Plaid Link update mode.</summary>
    RequiresReauth = 1,
    /// <summary>Connection access has been revoked or removed.</summary>
    Revoked = 2,
    Error = 3,
}

/// <summary>Direction of cash flow for a recurring suggestion detected by Plaid.</summary>
public enum RecurringFlowDirection
{
    /// <summary>Money flowing into the account (paychecks, deposits, interest).</summary>
    Inflow = 0,
    /// <summary>Money flowing out of the account (bills, subscriptions).</summary>
    Outflow = 1,
}
