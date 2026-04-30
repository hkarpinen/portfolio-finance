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

        // Optional tax withholding profile — columns are NULL when not configured.
        builder.OwnsOne(i => i.TaxProfile, tp =>
        {
            tp.Property(t => t.FilingStatus)
                .HasColumnName("tax_filing_status")
                .HasConversion<string>()
                .HasMaxLength(30);
            tp.Property(t => t.StateCode)
                .HasColumnName("tax_state_code")
                .HasMaxLength(2);
            tp.Property(t => t.FederalAllowances)
                .HasColumnName("tax_federal_allowances");
            tp.Property(t => t.StateAllowances)
                .HasColumnName("tax_state_allowances");
        });

        // Voluntary deductions stored as a JSON array column.
        builder.OwnsMany(i => i.Deductions, d =>
        {
            d.ToJson("deductions");
            d.Property(p => p.Type).HasConversion<string>();
            d.Property(p => p.Method).HasConversion<string>();
            d.Property(p => p.Label).HasMaxLength(200);
        });
    }
}
