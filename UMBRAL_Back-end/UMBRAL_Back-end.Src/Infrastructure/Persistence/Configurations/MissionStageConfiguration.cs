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

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Order)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.BaseScore)
            .IsRequired();

        // Trivia fields (nullable)
        builder.Property(s => s.Question)
            .HasMaxLength(1000);

        // TreasureHunt fields (nullable) — RB-20
        builder.Property(s => s.Latitude);
        builder.Property(s => s.Longitude);

        builder.Property(s => s.QrCode)
            .HasMaxLength(500);

        // Unique QR code constraint across all stages
        builder.HasIndex(s => s.QrCode)
            .IsUnique()
            .HasFilter("\"QrCode\" IS NOT NULL");

        // TriviaOptions navigation — EF Core uses _options backing field by convention
        builder.HasMany(s => s.Options)
            .WithOne()
            .HasForeignKey(o => o.StageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Options)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
