namespace StageService.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StageService.Domain.Stages;

public class TriviaOptionConfiguration : IEntityTypeConfiguration<TriviaOption>
{
    public void Configure(EntityTypeBuilder<TriviaOption> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Text).IsRequired().HasMaxLength(500);
        builder.Property(o => o.IsCorrect).IsRequired();
        builder.Property(o => o.StageId).IsRequired();
    }
}
