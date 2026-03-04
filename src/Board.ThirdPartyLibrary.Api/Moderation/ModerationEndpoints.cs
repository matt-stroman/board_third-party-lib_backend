using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Board.ThirdPartyLibrary.Api.Moderation;

/// <summary>
/// Maps moderator-only endpoints.
/// </summary>
internal static class ModerationEndpoints
{
    /// <summary>
    /// Maps moderator-only endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/moderation");

        group.MapGet("/developer-enrollment-requests", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.ListRequestsAsync(user.Claims, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentRequestListStatus.Success => Results.Ok(
                    new DeveloperEnrollmentRequestListResponse(result.Requests!.Select(MapRequest).ToArray())),
                DeveloperEnrollmentRequestListStatus.Forbidden => CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators can review developer enrollment requests.",
                    "moderator_access_required"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapPost("/developer-enrollment-requests/{requestId:guid}/approve", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.ReviewRequestAsync(
                user.Claims,
                requestId,
                DeveloperEnrollmentReviewDecision.Approve,
                null,
                [],
                cancellationToken);

            return MapReviewResult(result);
        });

        group.MapPost("/developer-enrollment-requests/{requestId:guid}/reject", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            [FromForm] ModerationMessageForm request,
            HttpRequest httpRequest,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var (attachments, attachmentErrors) = await DeveloperEnrollmentAttachmentReader.ReadAsync(form.Files, cancellationToken);
            if (attachmentErrors.Count > 0)
            {
                return Results.ValidationProblem(attachmentErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await developerEnrollmentService.ReviewRequestAsync(
                user.Claims,
                requestId,
                DeveloperEnrollmentReviewDecision.Reject,
                request.Message,
                attachments,
                cancellationToken);

            return MapReviewResult(result);
        }).DisableAntiforgery();

        group.MapPost("/developer-enrollment-requests/{requestId:guid}/request-more-information", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            [FromForm] ModerationMessageForm request,
            HttpRequest httpRequest,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var (attachments, attachmentErrors) = await DeveloperEnrollmentAttachmentReader.ReadAsync(form.Files, cancellationToken);
            if (attachmentErrors.Count > 0)
            {
                return Results.ValidationProblem(attachmentErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await developerEnrollmentService.ReviewRequestAsync(
                user.Claims,
                requestId,
                DeveloperEnrollmentReviewDecision.RequestMoreInformation,
                request.Message,
                attachments,
                cancellationToken);

            return MapReviewResult(result);
        }).DisableAntiforgery();

        group.MapGet("/developer-enrollment-requests/{requestId:guid}/conversation", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.GetModeratorConversationAsync(user.Claims, requestId, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentConversationStatus.Success => Results.Ok(new DeveloperEnrollmentConversationResponse(MapConversation(result.Conversation!))),
                DeveloperEnrollmentConversationStatus.Forbidden => CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators can review developer enrollment requests.",
                    "moderator_access_required"),
                DeveloperEnrollmentConversationStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/developer-enrollment-requests/{requestId:guid}/attachments/{attachmentId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            Guid attachmentId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.GetModeratorAttachmentAsync(user.Claims, requestId, attachmentId, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentAttachmentStatus.Success => Results.File(
                    result.Attachment!.Content,
                    result.Attachment.ContentType,
                    result.Attachment.FileName),
                DeveloperEnrollmentAttachmentStatus.Forbidden => CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators can review developer enrollment requests.",
                    "moderator_access_required"),
                DeveloperEnrollmentAttachmentStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static IResult MapReviewResult(DeveloperEnrollmentReviewResult result) =>
        result.Status switch
        {
            DeveloperEnrollmentReviewStatus.Success => Results.Ok(
                new DeveloperEnrollmentRequestResponse(MapRequest(result.Request!))),
            DeveloperEnrollmentReviewStatus.Forbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Moderator access is required.",
                "Only moderators can review developer enrollment requests.",
                "moderator_access_required"),
            DeveloperEnrollmentReviewStatus.NotFound => Results.NotFound(),
            DeveloperEnrollmentReviewStatus.Conflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Developer enrollment review conflict.",
                "Only requests waiting on moderator review can be reviewed.",
                "developer_enrollment_review_conflict"),
            DeveloperEnrollmentReviewStatus.Validation => Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["message"] = ["Moderator comments are required for this action."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity),
            DeveloperEnrollmentReviewStatus.UpstreamFailure => CreateProblemResult(
                StatusCodes.Status502BadGateway,
                "Developer enrollment could not be completed.",
                result.ErrorDetail ?? "Keycloak role assignment failed for the authenticated user.",
                "keycloak_developer_enrollment_failed"),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static DeveloperEnrollmentRequestDto MapRequest(DeveloperEnrollmentRequestSnapshot request) =>
        new(
            request.RequestId,
            request.ApplicantSubject,
            request.ApplicantDisplayName,
            request.ApplicantEmail,
            request.Status,
            request.ActionRequiredBy,
            request.DeveloperAccessEnabled,
            request.RequestedAtUtc,
            request.UpdatedAtUtc,
            request.ReviewedAtUtc,
            request.ReapplyAvailableAtUtc,
            request.ReviewerSubject);

    private static DeveloperEnrollmentConversation MapConversation(DeveloperEnrollmentConversationSnapshot snapshot) =>
        new(
            snapshot.RequestId,
            snapshot.Status,
            snapshot.ActionRequiredBy,
            snapshot.ReviewedAtUtc,
            snapshot.ReviewerSubject,
            snapshot.Messages.Select(message => new DeveloperEnrollmentConversationMessage(
                message.MessageId,
                message.AuthorRole,
                message.AuthorSubject,
                message.AuthorDisplayName,
                message.MessageKind,
                message.Body,
                message.CreatedAtUtc,
                message.Attachments.Select(attachment => new DeveloperEnrollmentConversationAttachment(
                    attachment.AttachmentId,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes)).ToArray())).ToArray());

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new ModerationProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);
}

/// <summary>
/// Moderator-visible developer enrollment request DTO.
/// </summary>
internal sealed record DeveloperEnrollmentRequestDto(
    Guid RequestId,
    string ApplicantSubject,
    string? ApplicantDisplayName,
    string? ApplicantEmail,
    string Status,
    string ActionRequiredBy,
    bool DeveloperAccessEnabled,
    DateTime RequestedAt,
    DateTime UpdatedAt,
    DateTime? ReviewedAt,
    DateTime? ReapplyAvailableAt,
    string? ReviewerSubject);

/// <summary>
/// Response wrapper for developer enrollment request lists.
/// </summary>
internal sealed record DeveloperEnrollmentRequestListResponse(IReadOnlyList<DeveloperEnrollmentRequestDto> Requests);

/// <summary>
/// Response wrapper for a reviewed developer enrollment request.
/// </summary>
internal sealed record DeveloperEnrollmentRequestResponse(DeveloperEnrollmentRequestDto DeveloperEnrollmentRequest);

internal sealed class ModerationMessageForm
{
    public string? Message { get; set; }

    public List<IFormFile> Attachments { get; set; } = [];
}

/// <summary>
/// Problem-details envelope used by moderation endpoints.
/// </summary>
internal sealed record ModerationProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
