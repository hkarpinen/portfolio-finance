using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, v => new HouseholdId(v));

        builder.Property(h => h.Name).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Description).HasMaxLength(1000);
        builder.Property(h => h.CurrencyCode).HasMaxLength(3).IsRequired();

        builder.Property(h => h.OwnerId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.UpdatedAt).IsRequired();
        builder.Property(h => h.IsActive).IsRequired();
    }
}
