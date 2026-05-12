using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> builder)
    {
        builder.ToTable("expense_splits");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, v => new ExpenseSplitId(v));

        builder.Property(s => s.ExpenseId)
            .HasConversion(id => id.Value, v => new ExpenseId(v));

        builder.Property(s => s.HouseholdId)
            .HasConversion(id => id.Value, v => new HouseholdId(v));

        builder.Property(s => s.MembershipId)
            .HasConversion(id => id.Value, v => new MembershipId(v));

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.ComplexProperty(s => s.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => s.ExpenseId);
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => new { s.UserId, s.ExpenseId });
    }
}
