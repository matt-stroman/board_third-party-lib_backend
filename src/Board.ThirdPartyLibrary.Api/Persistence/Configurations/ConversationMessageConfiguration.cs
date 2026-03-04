using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("conversation_messages", tableBuilder =>
        {
            tableBuilder.HasComment("Persisted messages within generic workflow conversations.");
        });

        builder.HasKey(message => message.Id)
            .HasName("pk_conversation_messages");

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.ThreadId)
            .HasColumnName("thread_id")
            .IsRequired();

        builder.Property(message => message.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(message => message.AuthorRole)
            .HasColumnName("author_role")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(message => message.MessageKind)
            .HasColumnName("message_kind")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(message => message.Body)
            .HasColumnName("body")
            .HasMaxLength(4000);

        builder.Property(message => message.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(message => new { message.ThreadId, message.CreatedAtUtc })
            .HasDatabaseName("ix_conversation_messages_thread_created_at");

        builder.HasOne(message => message.Thread)
            .WithMany(thread => thread.Messages)
            .HasForeignKey(message => message.ThreadId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_conversation_messages_threads");

        builder.HasOne(message => message.User)
            .WithMany(user => user.ConversationMessages)
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversation_messages_users");
    }
}
