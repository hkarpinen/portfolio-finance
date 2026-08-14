using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class LedgerConfiguration : IEntityTypeConfiguration<Ledger>
{
    public void Configure(EntityTypeBuilder<Ledger> builder)
    {
        builder.ToTable("ledgers");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, v => new LedgerId(v));

        // Same two columns as before: EntityKind's ordinals match the LedgerOwnerType they
        // replace (Household/Group = 0, Person/User = 1), so the stored rows are already right.
        builder.ComplexProperty(l => l.Owner, owner =>
        {
            owner.Property(o => o.Kind).HasColumnName("owner_type").HasConversion<int>().IsRequired();
            owner.Property(o => o.Id).HasColumnName("owner_id").IsRequired();
        });

        builder.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired();

        // One book per entity. Created in the migration with raw SQL: EF 8 cannot name a complex
        // property's columns in HasIndex.
    }
}
