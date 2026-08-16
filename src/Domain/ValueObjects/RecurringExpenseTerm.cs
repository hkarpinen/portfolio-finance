namespace Finance.Domain.ValueObjects;

/// <summary>
/// What a schedule holds from a given date onward.
///
/// A rent rise is a new version, not an edit. That is how an agreement actually changes, and it
/// means an expense generated for a date in the past picks up what was true THEN even if nobody had
/// got round to recording that month yet.
///
/// A bare decimal: the currency belongs to the schedule and cannot change halfway through an
/// agreement, so carrying one per version could only ever agree with it or be wrong.
/// </summary>
public sealed record RecurringExpenseTerm(DateTime EffectiveFrom, decimal Amount)
{
    public static RecurringExpenseTerm From(DateTime effectiveFrom, decimal amount) =>
        new(DateTime.SpecifyKind(effectiveFrom.Date, DateTimeKind.Utc), amount);
}
