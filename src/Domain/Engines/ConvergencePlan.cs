using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// What has to happen so the books say what a draft says, given what they say now.
///
/// Every posting path used to work this out for itself: fetch the entries under a source, compare
/// with a hand-written check, reverse what did not match, post the replacement. Eight copies of one
/// decision, and the checks differed by accident rather than intent. This is that decision, once.
/// </summary>
public sealed record ConvergencePlan(IReadOnlyList<JournalEntry> Reverse, EntryDraft? Post)
{
    /// <summary>True when the books already say it and nothing needs writing.</summary>
    public bool AlreadyThere => Reverse.Count == 0 && Post is null;

    /// <summary>
    /// The plan to make <paramref name="draft"/> true.
    ///
    /// Exactly one in-effect entry saying the same thing is the common case — a redelivered message
    /// or an edit that changed nothing — and it produces no work at all. Anything else is reversed
    /// and replaced, including the case of several entries under one source, which should not
    /// happen and is repaired here rather than left to accumulate.
    /// </summary>
    public static ConvergencePlan For(EntryDraft draft, IReadOnlyList<JournalEntry> inEffect)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (inEffect.Count == 1 && draft.AlreadySaidBy(inEffect[0]))
            return new ConvergencePlan([], null);

        return new ConvergencePlan(inEffect, draft);
    }

    /// <summary>
    /// The plan to take a source off the books entirely — the allocation was removed, the payment
    /// was undone. Nothing replaces it.
    /// </summary>
    public static ConvergencePlan Remove(IReadOnlyList<JournalEntry> inEffect) =>
        new(inEffect, null);
}
