using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class ConversationMessageAttachmentConfiguration : IEntityTypeConfiguration<ConversationMessageAttachment>
{
    public void Configure(EntityTypeBuilder<ConversationMessageAttachment> builder)
    {
        builder.ToTable("conversation_message_attachments", tableBuilder =>
        {
            tableBuilder.HasComment("Binary attachments uploaded as part of persisted conversation messages.");
        });

        builder.HasKey(attachment => attachment.Id)
            .HasName("pk_conversation_message_attachments");

        builder.Property(attachment => attachment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(attachment => attachment.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(attachment => attachment.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(attachment => attachment.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(attachment => attachment.Content)
            .HasColumnName("content")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(attachment => attachment.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(attachment => attachment.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne(attachment => attachment.Message)
            .WithMany(message => message.Attachments)
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_conversation_message_attachments_messages");
    }
}
