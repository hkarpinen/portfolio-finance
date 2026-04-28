using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// Reduces the boilerplate of registering strongly-typed ID value converters in
/// every <see cref="IEntityTypeConfiguration{T}"/>.  Instead of repeating
/// <c>.HasConversion(id =&gt; id.Value, v =&gt; new XxxId(v)).ValueGeneratedNever()</c>
/// on every property, call <c>.HasStronglyTypedIdConversion(v =&gt; new XxxId(v))</c>.
/// </summary>
internal static class PropertyBuilderExtensions
{
    /// <summary>
    /// Registers a round-trip <c>Guid</c> ↔ <typeparamref name="TId"/> converter
    /// and marks the property as not database-generated.
    /// </summary>
    public static PropertyBuilder<TId> HasStronglyTypedIdConversion<TId>(
        this PropertyBuilder<TId> builder,
        Func<Guid, TId> ctor)
        where TId : struct =>
        builder
            .HasConversion(id => GetValue(id), value => ctor(value))
            .ValueGeneratedNever();

    // Helper to extract the Guid value via reflection-free dynamic dispatch.
    // All strongly-typed IDs in this project expose a `Value` property of type Guid.
    private static Guid GetValue<TId>(TId id) where TId : struct =>
        (Guid)typeof(TId).GetProperty("Value")!.GetValue(id)!;
}
