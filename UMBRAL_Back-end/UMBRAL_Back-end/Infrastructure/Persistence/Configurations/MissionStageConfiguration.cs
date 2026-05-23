namespace UMBRAL_Back_end.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMBRAL_Back_end.Domain.Missions;

public class MissionStageConfiguration : IEntityTypeConfiguration<MissionStage>
{
    public void Configure(EntityTypeBuilder<MissionStage> builder)
    {
        builder.ToTable("MissionStages");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Order)
            .IsRequired();
    }
}
