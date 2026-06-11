namespace Finance.Domain.ValueObjects;

/// <summary>
/// Discriminator constants used when a suggestion or transaction is linked to a
/// domain entity. Using typed constants prevents string-literal drift across the
/// link/unlink/accept code paths.
/// </summary>
public static class LinkedEntityType
{
    public const string Charge = "Charge";
    public const string IncomeSource = "IncomeSource";
}
