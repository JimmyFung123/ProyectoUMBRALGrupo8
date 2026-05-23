namespace UMBRAL_Back_end.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UMBRAL_Back_end.Domain.Missions;

public class TriviaOptionConfiguration : IEntityTypeConfiguration<TriviaOption>
{
    public void Configure(EntityTypeBuilder<TriviaOption> builder)
    {
        builder.ToTable("TriviaOptions");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Text)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.IsCorrect)
            .IsRequired();
    }
}
