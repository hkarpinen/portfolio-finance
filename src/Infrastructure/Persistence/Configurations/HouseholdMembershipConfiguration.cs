using Bills.Domain.Aggregates;
using Bills.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class HouseholdMembershipConfiguration : IEntityTypeConfiguration<HouseholdMembership>
{
    public void Configure(EntityTypeBuilder<HouseholdMembership> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, v => new MembershipId(v));

        builder.Property(m => m.HouseholdId)
            .HasConversion(id => id.Value, v => new HouseholdId(v));

        builder.Property(m => m.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.InvitationCode).HasMaxLength(100);
        builder.Property(m => m.JoinedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
    }
}
