using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ExpensePaymentConfiguration : IEntityTypeConfiguration<ExpensePayment>
{
    public void Configure(EntityTypeBuilder<ExpensePayment> builder)
    {
        builder.ToTable("expense_payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, v => new ExpensePaymentId(v));

        builder.Property(p => p.ExpenseId)
            .HasConversion(id => id.Value, v => new ExpenseId(v))
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();

        builder.Property(p => p.OccurrenceDate).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.TransactionReference).HasMaxLength(500);

        builder.HasIndex(p => new { p.ExpenseId, p.OccurrenceDate }).IsUnique();
        builder.HasIndex(p => p.UserId);
    }
}
