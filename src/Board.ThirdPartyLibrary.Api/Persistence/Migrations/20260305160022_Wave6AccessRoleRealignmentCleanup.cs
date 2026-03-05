using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Board.ThirdPartyLibrary.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Wave6AccessRoleRealignmentCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_message_attachments");

            migrationBuilder.DropTable(
                name: "developer_enrollment_requests");

            migrationBuilder.DropTable(
                name: "user_notifications");

            migrationBuilder.DropTable(
                name: "conversation_messages");

            migrationBuilder.DropTable(
                name: "conversation_threads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    action_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    message_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                name: "developer_enrollment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_moderator_action_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_moderator_action_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reapply_available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Current workflow status: pending_review, awaiting_applicant_response, approved, rejected, or cancelled."),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_developer_enrollment_requests", x => x.id);
                    table.CheckConstraint("ck_developer_enrollment_requests_status", "status IN ('pending_review', 'awaiting_applicant_response', 'approved', 'rejected', 'cancelled')");
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_conversation_threads",
                        column: x => x.conversation_thread_id,
                        principalTable: "conversation_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_last_moderator_users",
                        column: x => x.last_moderator_action_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_reviewed_by_users",
                        column: x => x.reviewed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_developer_enrollment_requests_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Application-owned developer enrollment workflow state for player accounts.");

            migrationBuilder.CreateTable(
                name: "conversation_message_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
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
                name: "IX_developer_enrollment_requests_conversation_thread_id",
                table: "developer_enrollment_requests",
                column: "conversation_thread_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_developer_enrollment_requests_last_moderator_action_by_user~",
                table: "developer_enrollment_requests",
                column: "last_moderator_action_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_developer_enrollment_requests_reviewed_by_user_id",
                table: "developer_enrollment_requests",
                column: "reviewed_by_user_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_user_created_at",
                table: "user_notifications",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_notifications_user_is_read_created_at",
                table: "user_notifications",
                columns: new[] { "user_id", "is_read", "created_at" });
        }
    }
}
