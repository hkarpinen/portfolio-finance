using Finance.Domain.Aggregates;
namespace Finance.Domain.ValueObjects;

/// <summary>
/// An account a chart knows how to name and open, before anyone has checked whether the ledger
/// already has it.
///
/// The code and the factory belong together — a caller holding one without the other can look up
/// "1000" and open something that is not Cash. Pairing them is what stops the two drifting.
/// </summary>
public sealed record AccountSpec(string Code, Func<Account> Open);
