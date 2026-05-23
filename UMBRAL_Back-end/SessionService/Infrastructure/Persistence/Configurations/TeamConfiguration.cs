namespace SessionService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SessionService.Domain.Sessions;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.IsConnected)
            .IsRequired();

        builder.Property(t => t.CurrentStageOrder)
            .IsRequired();

        builder.Property(t => t.CluesReceivedCurrentStage)
            .IsRequired();

        builder.Property(t => t.TotalCluesReceived)
            .IsRequired();

        builder.HasIndex(t => t.SessionId);

        builder.ToTable("Teams");
    }
}
