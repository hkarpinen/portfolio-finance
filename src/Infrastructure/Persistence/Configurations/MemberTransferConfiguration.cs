using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class MemberTransferConfiguration : IEntityTypeConfiguration<MemberTransfer>
{
    public void Configure(EntityTypeBuilder<MemberTransfer> builder)
    {
        builder.ToTable("member_transfers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion(id => id.Value, v => new MemberTransferId(v));
        builder.Property(t => t.GroupId).HasConversion(id => id.Value, v => new GroupId(v));
        builder.Property(t => t.FromUserId).HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(t => t.ToUserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.ComplexProperty(t => t.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(t => t.OccurredOn).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        // The ledger source is derived from the id; nothing to store.
        builder.Ignore(t => t.LedgerSource);

        builder.HasIndex(t => new { t.GroupId, t.OccurredOn });
    }
}
