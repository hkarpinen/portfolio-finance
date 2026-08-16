using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>What has to happen so the books say what a draft says, given what they say now.</summary>
public sealed record ConvergencePlan(IReadOnlyList<JournalEntry> Reverse, JournalEntry? Post)
{
    public bool AlreadyThere => Reverse.Count == 0 && Post is null;

    /// <summary>
    /// The plan to make <paramref name="candidate"/> true.
    ///
    /// Exactly one in-effect entry saying the same thing is the common case — a redelivered message
    /// or an edit that changed nothing — and it produces no work at all. Anything else is reversed
    /// and replaced, including the case of several entries under one source, which should not
    /// happen and is repaired here rather than left to accumulate.
    /// </summary>
    public static ConvergencePlan For(JournalEntry candidate, IReadOnlyList<JournalEntry> inEffect)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (inEffect.Count == 1 && inEffect[0].SaysTheSameAs(candidate))
            return new ConvergencePlan([], null);

        return new ConvergencePlan(inEffect, candidate);
    }

    /// <summary>
    /// The plan to take a source off the books entirely — the share was removed, the payment
    /// was undone. Nothing replaces it.
    /// </summary>
    public static ConvergencePlan Remove(IReadOnlyList<JournalEntry> inEffect) =>
        new(inEffect, null);
}
