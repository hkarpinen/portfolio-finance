using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, v => new BillId(v));

        builder.Property(b => b.HouseholdId)
            .HasConversion(id => id.Value, v => new HouseholdId(v));

        builder.Property(b => b.CreatedBy)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(b => b.Title).HasMaxLength(300).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(b => b.DueDate).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();
        builder.Property(b => b.IsActive).IsRequired();

        builder.ComplexProperty(b => b.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsOne(b => b.RecurrenceSchedule, rs =>
        {
            rs.Property(r => r.Frequency).HasColumnName("recurrence_frequency").HasConversion<string>().HasMaxLength(50);
            rs.Property(r => r.StartDate).HasColumnName("recurrence_start_date");
            rs.Property(r => r.EndDate).HasColumnName("recurrence_end_date");
        });
    }
}
