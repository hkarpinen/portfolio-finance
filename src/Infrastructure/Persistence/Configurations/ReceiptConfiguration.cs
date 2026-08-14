using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new ReceiptId(v));

        builder.ComplexProperty(r => r.Owner, owner =>
        {
            owner.Property(o => o.Kind).HasColumnName("owner_kind").HasConversion<int>().IsRequired();
            owner.Property(o => o.Id).HasColumnName("owner_id").IsRequired();
        });

        builder.Property(r => r.IntoAccountId).IsRequired();
        builder.Property(r => r.Source).HasMaxLength(300).IsRequired();

        builder.ComplexProperty(r => r.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(r => r.ReceivedOn).IsRequired();
        builder.Property(r => r.IsVoid).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        // Derived from the id; nothing to store.
        builder.Ignore(r => r.LedgerSource);

        // Indexed on the owner columns in the migration with raw SQL: EF 8 cannot name a complex
        // property's columns in HasIndex.
        builder.HasIndex(r => r.ReceivedOn);
    }
}
