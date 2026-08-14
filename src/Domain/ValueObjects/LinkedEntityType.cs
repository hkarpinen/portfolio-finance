namespace Finance.Domain.ValueObjects;

public static class LinkedEntityType
{
    public const string Expense = "Expense";
    public const string IncomeSource = "IncomeSource";

    /// <summary>Money arriving, imported from a bank.</summary>
    public const string Receipt = "Receipt";
}
