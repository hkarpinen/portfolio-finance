using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ChargePaymentConfiguration : IEntityTypeConfiguration<ChargePayment>
{
    public void Configure(EntityTypeBuilder<ChargePayment> builder)
    {
        builder.ToTable("charge_payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new ChargePaymentId(v));

        builder.Property(p => p.ChargeId)
            .HasConversion(id => id.Value, v => new ChargeId(v))
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(p => p.OccurrenceDate).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.TransactionReference).HasMaxLength(500);

        builder.HasIndex(p => new { p.ChargeId, p.OccurrenceDate }).IsUnique();
        builder.HasIndex(p => p.UserId);
    }
}
