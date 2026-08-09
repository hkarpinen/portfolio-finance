namespace Finance.Domain.ValueObjects;

public sealed record DeductionLineItem(
    string Type,
    string Label,
    bool IsEmployerSponsored,
    decimal Amount,
    string Currency);
