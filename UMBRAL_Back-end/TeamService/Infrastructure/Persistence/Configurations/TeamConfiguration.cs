namespace TeamService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeamService.Domain.Teams;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.IsConnected).IsRequired();
        builder.Property(t => t.CurrentStageOrder).IsRequired();
        builder.Property(t => t.CluesReceivedCurrentStage).IsRequired();
        builder.Property(t => t.TotalCluesReceived).IsRequired();
        builder.Property(t => t.Score).IsRequired();
        builder.Property(t => t.ClueTimerResetAt).IsRequired(false);
        builder.Property(t => t.LastClueWasAutomatic).IsRequired();
        builder.HasIndex(t => t.SessionId);
        builder.Property(t => t.InviteCode).IsRequired().HasMaxLength(10);
        builder.Property(t => t.MemberCount).IsRequired();
        builder.HasIndex(t => t.InviteCode).IsUnique();
    }
}
