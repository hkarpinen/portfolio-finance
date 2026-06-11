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

        builder.Property(l => l.OwnerType).HasConversion<int>();
        builder.Property(l => l.OwnerId).IsRequired();
        builder.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired();

        // One ledger per owner (a group's book, a user's book).
        builder.HasIndex(l => new { l.OwnerType, l.OwnerId }).IsUnique();
    }
}
