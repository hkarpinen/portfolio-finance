using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class IncomeSourceConfiguration : IEntityTypeConfiguration<IncomeSource>
{
    public void Configure(EntityTypeBuilder<IncomeSource> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasConversion(id => id.Value, v => new IncomeId(v));

        builder.Property(i => i.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(i => i.Source).HasMaxLength(300).IsRequired();
        builder.Property(i => i.LastPaymentDate).IsRequired(false);
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();
        builder.Property(i => i.IsActive).IsRequired();

        builder.ComplexProperty(i => i.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(i => i.RecurrenceSchedule, rs =>
        {
            rs.Property(r => r.Frequency).HasColumnName("recurrence_frequency").HasConversion<string>().HasMaxLength(50).IsRequired();
            rs.Property(r => r.StartDate).HasColumnName("recurrence_start_date").IsRequired();
            rs.Property(r => r.EndDate).HasColumnName("recurrence_end_date");
        });
    }
}
