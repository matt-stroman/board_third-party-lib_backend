using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave6DeveloperEnrollmentConversationAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_developer_enrollment_requests_user_id",
                table: "developer_enrollment_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_developer_enrollment_requests_status",
                table: "developer_enrollment_requests");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "developer_enrollment_requests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "Current workflow status: pending_review, awaiting_applicant_response, approved, rejected, or cancelled.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "Current workflow status: pending, approved, or rejected.");

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "developer_enrollment_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "conversation_thread_id",
                table: "developer_enrollment_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_moderator_action_at",
                table: "developer_enrollment_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_moderator_action_by_user_id",
                table: "developer_enrollment_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reapply_available_at",
                table: "developer_enrollment_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "conversation_threads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_threads", x => x.id);
                },
                comment: "Generic persisted conversation threads attached to workflow records.");

            migrationBuilder.CreateTable(
                name: "user_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    action_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_notifications_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Generic in-app notifications targeted to local user projections.");

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_messages_threads",
                        column: x => x.thread_id,
                        principalTable: "conversation_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversation_messages_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "Persisted messages within generic workflow conversations.");

            migrationBuilder.CreateTable(
                name: "conversation_message_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_message_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_message_attachments_messages",
                        column: x => x.message_id,
                        principalTable: "conversation_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Binary attachments uploaded as part of persisted conversation messages.");

            migrationBuilder.Sql(
                """
                INSERT INTO conversation_threads (id, created_at, updated_at)
                SELECT id, requested_at, COALESCE(reviewed_at, requested_at)
                FROM developer_enrollment_requests;

                UPDATE developer_enrollment_requests
                SET
                    conversation_thread_id = id,
                    status = CASE WHEN status = 'pending' THEN 'pending_review' ELSE status END,
                    last_moderator_action_at = reviewed_at,
                    last_moderator_action_by_user_id = reviewed_by_user_id,
                    reapply_available_at = CASE
                        WHEN status = 'rejected' AND reviewed_at IS NOT NULL THEN reviewed_at + INTERVAL '30 days'
                        ELSE reapply_available_at
                    END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "conversation_thread_id",
                table: "developer_enrollment_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_developer_enrollment_requests_conversation_thread_id",
                table: "developer_enrollment_requests",
                column: "conversation_thread_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_developer_enrollment_requests_last_moderator_action_by_user~",
                table: "developer_enrollment_requests",
                column: "last_moderator_action_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_developer_enrollment_requests_user_requested_at",
                table: "developer_enrollment_requests",
                columns: new[] { "user_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ux_developer_enrollment_requests_user_open_request",
                table: "developer_enrollment_requests",
                column: "user_id",
                unique: true,
                filter: "status IN ('pending_review', 'awaiting_applicant_response')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_developer_enrollment_requests_status",
                table: "developer_enrollment_requests",
                sql: "status IN ('pending_review', 'awaiting_applicant_response', 'approved', 'rejected', 'cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_message_attachments_message_id",
                table: "conversation_message_attachments",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_messages_thread_created_at",
                table: "conversation_messages",
                columns: new[] { "thread_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_messages_user_id",
                table: "conversation_messages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_user_created_at",
                table: "user_notifications",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_user_is_read_created_at",
                table: "user_notifications",
                columns: new[] { "user_id", "is_read", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_developer_enrollment_requests_conversation_threads",
                table: "developer_enrollment_requests",
                column: "conversation_thread_id",
                principalTable: "conversation_threads",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_developer_enrollment_requests_last_moderator_users",
                table: "developer_enrollment_requests",
                column: "last_moderator_action_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_developer_enrollment_requests_conversation_threads",
                table: "developer_enrollment_requests");

            migrationBuilder.DropForeignKey(
                name: "fk_developer_enrollment_requests_last_moderator_users",
                table: "developer_enrollment_requests");

            migrationBuilder.DropTable(
                name: "conversation_message_attachments");

            migrationBuilder.DropTable(
                name: "user_notifications");

            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "conversation_threads");

            migrationBuilder.DropIndex(
                name: "IX_developer_enrollment_requests_conversation_thread_id",
                table: "developer_enrollment_requests");

            migrationBuilder.DropIndex(
                name: "IX_developer_enrollment_requests_last_moderator_action_by_user~",
                table: "developer_enrollment_requests");

            migrationBuilder.DropIndex(
                name: "ix_developer_enrollment_requests_user_requested_at",
                table: "developer_enrollment_requests");

            migrationBuilder.DropIndex(
                name: "ux_developer_enrollment_requests_user_open_request",
                table: "developer_enrollment_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_developer_enrollment_requests_status",
                table: "developer_enrollment_requests");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "developer_enrollment_requests");

            migrationBuilder.DropColumn(
                name: "conversation_thread_id",
                table: "developer_enrollment_requests");

            migrationBuilder.DropColumn(
                name: "last_moderator_action_at",
                table: "developer_enrollment_requests");

            migrationBuilder.DropColumn(
                name: "last_moderator_action_by_user_id",
                table: "developer_enrollment_requests");

            migrationBuilder.DropColumn(
                name: "reapply_available_at",
                table: "developer_enrollment_requests");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "developer_enrollment_requests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                comment: "Current workflow status: pending, approved, or rejected.",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldComment: "Current workflow status: pending_review, awaiting_applicant_response, approved, rejected, or cancelled.");

            migrationBuilder.Sql(
                """
                UPDATE developer_enrollment_requests
                SET status = CASE
                    WHEN status = 'pending_review' THEN 'pending'
                    WHEN status = 'awaiting_applicant_response' THEN 'pending'
                    ELSE status
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_developer_enrollment_requests_user_id",
                table: "developer_enrollment_requests",
                column: "user_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_developer_enrollment_requests_status",
                table: "developer_enrollment_requests",
                sql: "status IN ('pending', 'approved', 'rejected')");
        }
    }
}
