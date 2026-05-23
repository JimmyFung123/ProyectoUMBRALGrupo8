namespace UMBRAL_Back_end.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMBRAL_Back_end.Domain.Missions;

public class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(m => m.Name)
            .IsUnique();

        builder.Property(m => m.Description)
            .HasMaxLength(1000);

        builder.Property(m => m.Difficulty)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.MaxDuration)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasMany(m => m.Stages)
            .WithOne()
            .HasForeignKey(s => s.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(m => m.Stages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
