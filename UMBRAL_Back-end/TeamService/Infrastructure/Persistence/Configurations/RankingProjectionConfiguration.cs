namespace TeamService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamService.Domain.Rankings;

/// <summary>
/// EF mapping for the CQRS read-model table (HU-24). Lives in the same physical
/// database as <c>Teams</c> for operational simplicity, but is logically a
/// separate projection: only the projector writes here, and the query handler
/// reads it with no joins or recomputation.
/// </summary>
public class RankingProjectionConfiguration : IEntityTypeConfiguration<RankingProjection>
{
    public void Configure(EntityTypeBuilder<RankingProjection> builder)
    {
        builder.ToTable("RankingProjections");

        // Composite key — one row per (session, team).
        builder.HasKey(p => new { p.SessionId, p.TeamId });

        builder.Property(p => p.TeamName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Score).IsRequired();
        builder.Property(p => p.Rank).IsRequired();
        builder.Property(p => p.Position).IsRequired();
        builder.Property(p => p.CurrentStageOrder).IsRequired();
        builder.Property(p => p.IsConnected).IsRequired();
        builder.Property(p => p.LastStageCompletedAt).IsRequired(false);
        builder.Property(p => p.UpdatedAt).IsRequired();

        // The read path filters by SessionId and orders by Position — this
        // index lets PostgreSQL skip the sort and stream rows in physical order.
        builder.HasIndex(p => new { p.SessionId, p.Position });
    }
}
