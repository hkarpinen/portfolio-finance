using Finance.Domain.Events;

namespace Infrastructure.Messaging;

// The one list of events this service puts on the bus.
//
// Both ends read it: the outbox refuses anything absent from it, and the publisher resolves the
// wire type from it. That is what makes a drained-but-unpublishable event impossible — an event
// with no entry never reaches the outbox, so it cannot sit there dead-lettering. Wiring up a new
// consumer is one line here.
//
// Publishing the domain-event records directly relies on MassTransit's namespace+name routing: a
// consumer must declare a matching type in the Finance.Domain.Events namespace, or it binds a
// different exchange and misses every message silently. The EventType column stores
// GetType().Name, so the key is the simple class name.
internal static class PublishedEvents
{
    private static readonly Dictionary<string, Type> ByName = new()
    {
        [nameof(ExpenseCreated)] = typeof(ExpenseCreated),
        [nameof(ExpenseUpdated)] = typeof(ExpenseUpdated),
        [nameof(ExpenseDeactivated)] = typeof(ExpenseDeactivated),
        [nameof(ExpenseActivated)] = typeof(ExpenseActivated),
        [nameof(ExpensePaid)] = typeof(ExpensePaid),
        [nameof(ExpenseUnpaid)] = typeof(ExpenseUnpaid),
        [nameof(VendorPaid)] = typeof(VendorPaid),
        [nameof(VendorPaymentReversed)] = typeof(VendorPaymentReversed),
        [nameof(ShareCreated)] = typeof(ShareCreated),
        [nameof(ShareUpdated)] = typeof(ShareUpdated),
        [nameof(ShareRemoved)] = typeof(ShareRemoved),
        [nameof(SettlementRecorded)] = typeof(SettlementRecorded),
        [nameof(SettlementReversed)] = typeof(SettlementReversed),
    };

    internal static bool Includes(Type eventType) => ByName.ContainsKey(eventType.Name);

    internal static bool TryResolve(string eventTypeName, out Type wireType) =>
        ByName.TryGetValue(eventTypeName, out wireType!);

    internal static IReadOnlyCollection<string> Names => ByName.Keys;
}
