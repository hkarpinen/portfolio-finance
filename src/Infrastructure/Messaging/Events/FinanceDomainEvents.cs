namespace Infrastructure.Messaging.Events;

// Finance's own domain events are published directly over MassTransit as the
// records defined in Finance.Domain.Events — there is no separate integration
// shape on the publish side. See OutboxPublisher.EventTypeMap for the list of
// types this service publishes and OutboxExtensions.JsonOptions for the wire
// format (camelCase, value-object IDs flattened to GUIDs, Money kept nested).
//
// Cross-service consumers (e.g. household) declare matching record types in
// the Finance.Domain.Events namespace on their side so MassTransit's
// namespace+type routing lines up — no integration-event mapping is needed.
