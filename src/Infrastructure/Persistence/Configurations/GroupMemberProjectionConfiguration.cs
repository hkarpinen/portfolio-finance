using Finance.Infrastructure.Persistence.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class GroupMemberProjectionConfiguration : IEntityTypeConfiguration<GroupMemberProjection>
{
    public void Configure(EntityTypeBuilder<GroupMemberProjection> builder)
    {
        builder.ToTable("group_member_projections");

        // One row per person per group: leaving and rejoining mutates the same row rather than adding one.
        builder.HasKey(m => new { m.GroupId, m.UserId });

        builder.Property(m => m.Role).HasMaxLength(50).IsRequired();
        builder.Property(m => m.IsActive).IsRequired();
        builder.Property(m => m.JoinedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        builder.HasIndex(m => m.UserId);
    }
}
