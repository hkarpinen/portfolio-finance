namespace Finance.Domain.ValueObjects;

public readonly record struct FinancialConnectionId(Guid Value)
{
    public static FinancialConnectionId New() => new(Guid.NewGuid());
    public static FinancialConnectionId Create(Guid value) => new(value);
}

public enum FinancialConnectionStatus
{
    Healthy = 0,
    // Recovers only by the user re-authenticating through Plaid Link update mode.
    RequiresReauth = 1,
    Revoked = 2,
    Error = 3,
}

public enum RecurringFlowDirection
{
    Inflow = 0,
    Outflow = 1,
}
