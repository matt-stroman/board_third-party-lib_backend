using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class ConversationThreadConfiguration : IEntityTypeConfiguration<ConversationThread>
{
    public void Configure(EntityTypeBuilder<ConversationThread> builder)
    {
        builder.ToTable("conversation_threads", tableBuilder =>
        {
            tableBuilder.HasComment("Generic persisted conversation threads attached to workflow records.");
        });

        builder.HasKey(thread => thread.Id)
            .HasName("pk_conversation_threads");

        builder.Property(thread => thread.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(thread => thread.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(thread => thread.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
