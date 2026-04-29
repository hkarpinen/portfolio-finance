using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class BillSplitConfiguration : IEntityTypeConfiguration<BillSplit>
{
    public void Configure(EntityTypeBuilder<BillSplit> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, v => new SplitId(v));

        builder.Property(s => s.BillId)
            .HasConversion(id => id.Value, v => new BillId(v));

        builder.Property(s => s.HouseholdId)
            .HasConversion(id => id.Value, v => new HouseholdId(v));

        builder.Property(s => s.MembershipId)
            .HasConversion(id => id.Value, v => new MembershipId(v));

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(s => s.ClaimedBy)
            .HasConversion(id => id.HasValue ? id.Value.Value : (Guid?)null, v => v.HasValue ? new UserId(v.Value) : (UserId?)null);

        builder.ComplexProperty(s => s.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(s => s.IsClaimed).IsRequired();
        builder.Property(s => s.ClaimedAt);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();
    }
}
