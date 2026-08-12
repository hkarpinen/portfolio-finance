using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class ChargeScheduleConfiguration : IEntityTypeConfiguration<ChargeSchedule>
{
    public void Configure(EntityTypeBuilder<ChargeSchedule> builder)
    {
        builder.ToTable("charge_schedules");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion(id => id.Value, v => new ChargeScheduleId(v));

        builder.Property(s => s.UserId).HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(s => s.GroupId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new GroupId(v.Value) : (GroupId?)null)
            .IsRequired(false);

        builder.Property(s => s.CreatedBy)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new UserId(v.Value) : (UserId?)null)
            .IsRequired(false);

        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3);
        builder.Property(s => s.Title).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Description).HasMaxLength(2000);
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.FundingSource).HasConversion<string>().HasMaxLength(30).IsRequired();

        // Amount is derived from the versions, never stored — the same rule the ledger follows
        // for balances, for the same reason: a stored copy can disagree with what produced it.
        builder.Ignore(s => s.Amount);

        builder.OwnsMany(s => s.Amounts, a =>
        {
            a.ToTable("charge_schedule_amounts");
            a.WithOwner().HasForeignKey("schedule_id");
            a.Property(x => x.EffectiveFrom).HasColumnName("effective_from").IsRequired();
            a.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
            a.HasKey("schedule_id", nameof(ScheduledAmount.EffectiveFrom));
        });
        builder.Navigation(s => s.Amounts).AutoInclude();

        // The anchor and interval. Nothing here is a date a charge exists on — those are computed.
        builder.OwnsOne(s => s.Recurrence, r =>
        {
            r.Property(x => x.Frequency).HasColumnName("frequency").HasConversion<string>().HasMaxLength(50).IsRequired();
            r.Property(x => x.StartDate).HasColumnName("anchor_date").IsRequired();
            r.Property(x => x.EndDate).HasColumnName("end_date");
        });
        builder.Navigation(s => s.Recurrence).IsRequired();

        builder.HasIndex(s => s.GroupId);
        builder.HasIndex(s => s.UserId);

    }
}
