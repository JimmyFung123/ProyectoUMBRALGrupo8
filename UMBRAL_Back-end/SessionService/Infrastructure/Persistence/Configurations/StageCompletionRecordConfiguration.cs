namespace SessionService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionService.Domain.Statistics;

/// <summary>
/// EF mapping for the analytics fact table (HU-25). The two indexes target
/// the dashboard's most expensive query path: filter by
/// <c>IncludedInStatistics=true</c> (and optionally <c>MissionId</c>) and
/// then GROUP BY <c>StageOrder</c>.
/// </summary>
public class StageCompletionRecordConfiguration : IEntityTypeConfiguration<StageCompletionRecord>
{
    public void Configure(EntityTypeBuilder<StageCompletionRecord> builder)
    {
        builder.ToTable("StageCompletionRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.SessionId).IsRequired();
        builder.Property(r => r.MissionId).IsRequired();
        builder.Property(r => r.TeamId).IsRequired();
        builder.Property(r => r.StageOrder).IsRequired();
        builder.Property(r => r.StageType).IsRequired().HasMaxLength(40);
        builder.Property(r => r.ElapsedSeconds).IsRequired();
        builder.Property(r => r.WasCorrect).IsRequired(false);
        builder.Property(r => r.WasForceAdvance).IsRequired();
        builder.Property(r => r.RecordedAt).IsRequired();
        builder.Property(r => r.IncludedInStatistics).IsRequired();

        // Dashboard's main filter+group path.
        builder.HasIndex(r => new { r.IncludedInStatistics, r.MissionId, r.StageOrder });

        // Used by FinalizeSession's bulk UPDATE.
        builder.HasIndex(r => r.SessionId);
    }
}
