using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Board.ThirdPartyLibrary.Api.Persistence.Configurations;

internal sealed class DeveloperEnrollmentRequestConfiguration : IEntityTypeConfiguration<DeveloperEnrollmentRequest>
{
    public void Configure(EntityTypeBuilder<DeveloperEnrollmentRequest> builder)
    {
        builder.ToTable("developer_enrollment_requests", tableBuilder =>
        {
            tableBuilder.HasComment("Application-owned developer enrollment workflow state for player accounts.");
            tableBuilder.HasCheckConstraint(
                "ck_developer_enrollment_requests_status",
                $"status IN ('{DeveloperEnrollmentStatuses.PendingReview}', '{DeveloperEnrollmentStatuses.AwaitingApplicantResponse}', '{DeveloperEnrollmentStatuses.Approved}', '{DeveloperEnrollmentStatuses.Rejected}', '{DeveloperEnrollmentStatuses.Cancelled}')");
        });

        builder.HasKey(request => request.Id)
            .HasName("pk_developer_enrollment_requests");

        builder.Property(request => request.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(request => request.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired()
            .HasComment("Current workflow status: pending_review, awaiting_applicant_response, approved, rejected, or cancelled.");

        builder.Property(request => request.ConversationThreadId)
            .HasColumnName("conversation_thread_id")
            .IsRequired();

        builder.Property(request => request.RequestedAtUtc)
            .HasColumnName("requested_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.ReapplyAvailableAtUtc)
            .HasColumnName("reapply_available_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.CancelledAtUtc)
            .HasColumnName("cancelled_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.LastModeratorActionAtUtc)
            .HasColumnName("last_moderator_action_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.LastModeratorActionByUserId)
            .HasColumnName("last_moderator_action_by_user_id");

        builder.Property(request => request.ReviewedAtUtc)
            .HasColumnName("reviewed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(request => request.ReviewedByUserId)
            .HasColumnName("reviewed_by_user_id");

        builder.Property(request => request.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(request => request.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(request => new { request.UserId, request.RequestedAtUtc })
            .HasDatabaseName("ix_developer_enrollment_requests_user_requested_at");

        builder.HasIndex(request => request.UserId)
            .IsUnique()
            .HasFilter($"status IN ('{DeveloperEnrollmentStatuses.PendingReview}', '{DeveloperEnrollmentStatuses.AwaitingApplicantResponse}')")
            .HasDatabaseName("ux_developer_enrollment_requests_user_open_request");

        builder.HasOne(request => request.ConversationThread)
            .WithOne(thread => thread.DeveloperEnrollmentRequest)
            .HasForeignKey<DeveloperEnrollmentRequest>(request => request.ConversationThreadId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_developer_enrollment_requests_conversation_threads");

        builder.HasOne(request => request.User)
            .WithMany(user => user.DeveloperEnrollmentRequests)
            .HasForeignKey(request => request.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_developer_enrollment_requests_users");

        builder.HasOne(request => request.LastModeratorActionByUser)
            .WithMany(user => user.LastModeratedDeveloperEnrollmentRequests)
            .HasForeignKey(request => request.LastModeratorActionByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_developer_enrollment_requests_last_moderator_users");

        builder.HasOne(request => request.ReviewedByUser)
            .WithMany(user => user.ReviewedDeveloperEnrollmentRequests)
            .HasForeignKey(request => request.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_developer_enrollment_requests_reviewed_by_users");
    }
}
