using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

internal interface IDeveloperEnrollmentService
{
    Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentStateSnapshot> SubmitEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentMutationResult> CancelEnrollmentAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentRequestListResult> ListRequestsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentReviewResult> ReviewRequestAsync(IEnumerable<Claim> claims, Guid requestId, DeveloperEnrollmentReviewDecision decision, string? message, IReadOnlyList<ConversationAttachmentDraft> attachments, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentMutationResult> ReplyToEnrollmentAsync(IEnumerable<Claim> claims, Guid requestId, string? message, IReadOnlyList<ConversationAttachmentDraft> attachments, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentConversationResult> GetCurrentEnrollmentConversationAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentConversationResult> GetModeratorConversationAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentAttachmentResult> GetCurrentEnrollmentAttachmentAsync(IEnumerable<Claim> claims, Guid requestId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentAttachmentResult> GetModeratorAttachmentAsync(IEnumerable<Claim> claims, Guid requestId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<NotificationListResult> ListNotificationsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<NotificationMutationResult> MarkNotificationReadAsync(IEnumerable<Claim> claims, Guid notificationId, CancellationToken cancellationToken = default);
    Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
}

internal sealed class DeveloperEnrollmentService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService,
    IKeycloakUserRoleClient keycloakUserRoleClient) : IDeveloperEnrollmentService
{
    private const int RejectionProbationDays = 30;

    public async Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetLatestRequestQuery(actor.Id, true).SingleOrDefaultAsync(cancellationToken);
        return MapCurrentEnrollment(request, HasImmediateDeveloperAccess(claims));
    }

    public async Task<DeveloperEnrollmentStateSnapshot> SubmitEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetLatestRequestQuery(actor.Id, false).SingleOrDefaultAsync(cancellationToken);

        if (HasImmediateDeveloperAccess(claims))
        {
            return MapCurrentEnrollment(request, true);
        }

        if (request is not null && !CanCreateNewRequest(request))
        {
            return MapCurrentEnrollment(request, false);
        }

        var now = DateTime.UtcNow;
        var thread = new ConversationThread { Id = Guid.NewGuid(), CreatedAtUtc = now, UpdatedAtUtc = now };
        request = new DeveloperEnrollmentRequest
        {
            Id = Guid.NewGuid(),
            UserId = actor.Id,
            Status = DeveloperEnrollmentStatuses.PendingReview,
            ConversationThreadId = thread.Id,
            ConversationThread = thread,
            RequestedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.ConversationThreads.Add(thread);
        dbContext.DeveloperEnrollmentRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCurrentEnrollment(request, false);
    }

    public async Task<DeveloperEnrollmentMutationResult> CancelEnrollmentAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetRequestWithUsersQuery(false).SingleOrDefaultAsync(candidate => candidate.Id == requestId && candidate.UserId == actor.Id, cancellationToken);
        if (request is null)
        {
            return new(DeveloperEnrollmentMutationStatus.NotFound);
        }

        if (!IsOpenRequest(request.Status))
        {
            return new(DeveloperEnrollmentMutationStatus.Conflict);
        }

        var now = DateTime.UtcNow;
        request.Status = DeveloperEnrollmentStatuses.Cancelled;
        request.CancelledAtUtc = now;
        request.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(DeveloperEnrollmentMutationStatus.Success, MapCurrentEnrollment(request, false));
    }

    public async Task<DeveloperEnrollmentRequestListResult> ListRequestsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new(DeveloperEnrollmentRequestListStatus.Forbidden);
        }

        await EnsureActorAsync(claims, cancellationToken);
        var requests = await GetRequestWithUsersQuery(true)
            .OrderBy(candidate => candidate.Status == DeveloperEnrollmentStatuses.PendingReview ? 0 : candidate.Status == DeveloperEnrollmentStatuses.AwaitingApplicantResponse ? 1 : 2)
            .ThenByDescending(candidate => candidate.UpdatedAtUtc)
            .Select(candidate => new DeveloperEnrollmentRequestSnapshot(
                candidate.Id,
                candidate.User.KeycloakSubject,
                candidate.User.DisplayName,
                candidate.User.Email,
                candidate.Status,
                GetActionRequiredBy(candidate.Status),
                candidate.Status == DeveloperEnrollmentStatuses.Approved,
                candidate.RequestedAtUtc,
                candidate.UpdatedAtUtc,
                candidate.ReviewedAtUtc,
                candidate.ReapplyAvailableAtUtc,
                candidate.ReviewedByUser == null ? null : candidate.ReviewedByUser.KeycloakSubject))
            .ToListAsync(cancellationToken);
        return new(DeveloperEnrollmentRequestListStatus.Success, requests);
    }

    public async Task<DeveloperEnrollmentReviewResult> ReviewRequestAsync(IEnumerable<Claim> claims, Guid requestId, DeveloperEnrollmentReviewDecision decision, string? message, IReadOnlyList<ConversationAttachmentDraft> attachments, CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new(DeveloperEnrollmentReviewStatus.Forbidden);
        }

        var reviewer = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetRequestWithConversationQuery(false).SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        if (request is null)
        {
            return new(DeveloperEnrollmentReviewStatus.NotFound);
        }

        if (!string.Equals(request.Status, DeveloperEnrollmentStatuses.PendingReview, StringComparison.Ordinal))
        {
            return new(DeveloperEnrollmentReviewStatus.Conflict);
        }

        if (decision != DeveloperEnrollmentReviewDecision.Approve && string.IsNullOrWhiteSpace(message))
        {
            return new(DeveloperEnrollmentReviewStatus.Validation, ValidationCode: "message_required");
        }

        if (decision == DeveloperEnrollmentReviewDecision.Approve)
        {
            var roleAssignment = await keycloakUserRoleClient.EnsureRealmRoleAssignedAsync(request.User.KeycloakSubject, "developer", cancellationToken);
            if (!roleAssignment.Succeeded)
            {
                return new(DeveloperEnrollmentReviewStatus.UpstreamFailure, ErrorDetail: roleAssignment.ErrorDetail ?? "Keycloak role assignment failed for the authenticated user.");
            }
        }

        var now = DateTime.UtcNow;
        if (decision != DeveloperEnrollmentReviewDecision.Approve)
        {
            AddConversationMessage(
                request.ConversationThread,
                reviewer,
                ConversationAuthorRoles.Moderator,
                decision == DeveloperEnrollmentReviewDecision.Reject ? ConversationMessageKinds.ModeratorRejection : ConversationMessageKinds.ModeratorInformationRequest,
                message,
                attachments,
                now);
        }

        request.LastModeratorActionAtUtc = now;
        request.LastModeratorActionByUserId = reviewer.Id;
        request.LastModeratorActionByUser = reviewer;
        request.UpdatedAtUtc = now;

        if (decision == DeveloperEnrollmentReviewDecision.Approve)
        {
            request.Status = DeveloperEnrollmentStatuses.Approved;
            request.ReviewedAtUtc = now;
            request.ReviewedByUserId = reviewer.Id;
            request.ReviewedByUser = reviewer;
            request.ReapplyAvailableAtUtc = null;
            CreateNotification(request.UserId, "Developer access approved", "Your developer access request was approved. The developer console is now available.", "/develop", now);
        }
        else if (decision == DeveloperEnrollmentReviewDecision.Reject)
        {
            request.Status = DeveloperEnrollmentStatuses.Rejected;
            request.ReviewedAtUtc = now;
            request.ReviewedByUserId = reviewer.Id;
            request.ReviewedByUser = reviewer;
            request.ReapplyAvailableAtUtc = now.AddDays(RejectionProbationDays);
            CreateNotification(request.UserId, "Developer access rejected", $"Your developer access request was rejected. You may apply again after {request.ReapplyAvailableAtUtc:MMMM d, yyyy 'at' h:mm tt 'UTC'}.", "/account/developer-access", now);
        }
        else
        {
            request.Status = DeveloperEnrollmentStatuses.AwaitingApplicantResponse;
            request.ReviewedAtUtc = null;
            request.ReviewedByUserId = null;
            request.ReviewedByUser = null;
            request.ReapplyAvailableAtUtc = null;
            CreateNotification(request.UserId, "More information requested", "A moderator requested additional information for your developer access request.", "/account/developer-access", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new(DeveloperEnrollmentReviewStatus.Success, MapRequest(request));
    }

    public async Task<DeveloperEnrollmentMutationResult> ReplyToEnrollmentAsync(IEnumerable<Claim> claims, Guid requestId, string? message, IReadOnlyList<ConversationAttachmentDraft> attachments, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetRequestWithConversationQuery(false).SingleOrDefaultAsync(candidate => candidate.Id == requestId && candidate.UserId == actor.Id, cancellationToken);
        if (request is null)
        {
            return new(DeveloperEnrollmentMutationStatus.NotFound);
        }

        if (!string.Equals(request.Status, DeveloperEnrollmentStatuses.AwaitingApplicantResponse, StringComparison.Ordinal))
        {
            return new(DeveloperEnrollmentMutationStatus.Conflict);
        }

        if (string.IsNullOrWhiteSpace(message) && attachments.Count == 0)
        {
            return new(DeveloperEnrollmentMutationStatus.Validation, ValidationCode: "message_or_attachment_required");
        }

        var now = DateTime.UtcNow;
        AddConversationMessage(request.ConversationThread, actor, ConversationAuthorRoles.Applicant, ConversationMessageKinds.ApplicantReply, message, attachments, now);
        request.Status = DeveloperEnrollmentStatuses.PendingReview;
        request.UpdatedAtUtc = now;
        if (request.LastModeratorActionByUserId.HasValue && request.LastModeratorActionByUserId != actor.Id)
        {
            CreateNotification(request.LastModeratorActionByUserId.Value, "Applicant replied with more information", $"{(request.User.DisplayName ?? request.User.KeycloakSubject)} replied to a developer access information request.", "/moderation/developer-enrollment-requests", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new(DeveloperEnrollmentMutationStatus.Success, MapCurrentEnrollment(request, false));
    }

    public async Task<DeveloperEnrollmentConversationResult> GetCurrentEnrollmentConversationAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var request = await GetConversationRequestQuery(true).SingleOrDefaultAsync(candidate => candidate.Id == requestId && candidate.UserId == actor.Id, cancellationToken);
        return request is null ? new(DeveloperEnrollmentConversationStatus.NotFound) : new(DeveloperEnrollmentConversationStatus.Success, MapConversation(request));
    }

    public async Task<DeveloperEnrollmentConversationResult> GetModeratorConversationAsync(IEnumerable<Claim> claims, Guid requestId, CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new(DeveloperEnrollmentConversationStatus.Forbidden);
        }

        await EnsureActorAsync(claims, cancellationToken);
        var request = await GetConversationRequestQuery(true).SingleOrDefaultAsync(candidate => candidate.Id == requestId, cancellationToken);
        return request is null ? new(DeveloperEnrollmentConversationStatus.NotFound) : new(DeveloperEnrollmentConversationStatus.Success, MapConversation(request));
    }

    public Task<DeveloperEnrollmentAttachmentResult> GetCurrentEnrollmentAttachmentAsync(IEnumerable<Claim> claims, Guid requestId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        GetAttachmentAsync(claims, requestId, attachmentId, false, cancellationToken);

    public Task<DeveloperEnrollmentAttachmentResult> GetModeratorAttachmentAsync(IEnumerable<Claim> claims, Guid requestId, Guid attachmentId, CancellationToken cancellationToken = default) =>
        GetAttachmentAsync(claims, requestId, attachmentId, true, cancellationToken);

    public async Task<NotificationListResult> ListNotificationsAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var notifications = await dbContext.UserNotifications
            .AsNoTracking()
            .Where(candidate => candidate.UserId == actor.Id)
            .OrderBy(candidate => candidate.IsRead)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .Select(candidate => new NotificationSnapshot(candidate.Id, candidate.Category, candidate.Title, candidate.Body, candidate.ActionUrl, candidate.IsRead, candidate.CreatedAtUtc, candidate.ReadAtUtc))
            .ToListAsync(cancellationToken);
        return new(notifications);
    }

    public async Task<NotificationMutationResult> MarkNotificationReadAsync(IEnumerable<Claim> claims, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var notification = await dbContext.UserNotifications.SingleOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.UserId == actor.Id, cancellationToken);
        if (notification is null)
        {
            return new(NotificationMutationStatus.NotFound);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            notification.UpdatedAtUtc = notification.ReadAtUtc.Value;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new(NotificationMutationStatus.Success, new NotificationSnapshot(notification.Id, notification.Category, notification.Title, notification.Body, notification.ActionUrl, notification.IsRead, notification.CreatedAtUtc, notification.ReadAtUtc));
    }

    public async Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        if (HasImmediateDeveloperAccess(claims))
        {
            return true;
        }

        var actor = await EnsureActorAsync(claims, cancellationToken);
        var status = await GetLatestRequestQuery(actor.Id, true).Select(candidate => candidate.Status).SingleOrDefaultAsync(cancellationToken);
        return string.Equals(status, DeveloperEnrollmentStatuses.Approved, StringComparison.Ordinal);
    }

    private async Task<DeveloperEnrollmentAttachmentResult> GetAttachmentAsync(IEnumerable<Claim> claims, Guid requestId, Guid attachmentId, bool requireModerator, CancellationToken cancellationToken)
    {
        if (requireModerator && !HasModeratorAccess(claims))
        {
            return new(DeveloperEnrollmentAttachmentStatus.Forbidden);
        }

        var actor = await EnsureActorAsync(claims, cancellationToken);
        var attachment = await dbContext.ConversationMessageAttachments
            .AsNoTracking()
            .Where(candidate => candidate.Id == attachmentId)
            .Select(candidate => new
            {
                Attachment = candidate,
                RequestId = candidate.Message.Thread.DeveloperEnrollmentRequest!.Id,
                UserId = candidate.Message.Thread.DeveloperEnrollmentRequest!.UserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null || attachment.RequestId != requestId || (!requireModerator && attachment.UserId != actor.Id))
        {
            return new(DeveloperEnrollmentAttachmentStatus.NotFound);
        }

        return new(DeveloperEnrollmentAttachmentStatus.Success, new AttachmentPayloadSnapshot(attachment.Attachment.FileName, attachment.Attachment.ContentType, attachment.Attachment.Content));
    }

    private IQueryable<DeveloperEnrollmentRequest> GetLatestRequestQuery(Guid userId, bool asNoTracking) =>
        GetRequestWithUsersQuery(asNoTracking).Where(candidate => candidate.UserId == userId).OrderByDescending(candidate => candidate.RequestedAtUtc).Take(1);

    private IQueryable<DeveloperEnrollmentRequest> GetRequestWithUsersQuery(bool asNoTracking)
    {
        var query = dbContext.DeveloperEnrollmentRequests.Include(candidate => candidate.User).Include(candidate => candidate.ReviewedByUser);
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<DeveloperEnrollmentRequest> GetRequestWithConversationQuery(bool asNoTracking)
    {
        var query = dbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.ReviewedByUser)
            .Include(candidate => candidate.ConversationThread)
                .ThenInclude(thread => thread.Messages)
                    .ThenInclude(message => message.Attachments);
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private IQueryable<DeveloperEnrollmentRequest> GetConversationRequestQuery(bool asNoTracking)
    {
        var query = dbContext.DeveloperEnrollmentRequests
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.ReviewedByUser)
            .Include(candidate => candidate.ConversationThread)
                .ThenInclude(thread => thread.Messages)
                    .ThenInclude(message => message.User)
            .Include(candidate => candidate.ConversationThread)
                .ThenInclude(thread => thread.Messages)
                    .ThenInclude(message => message.Attachments);
        return asNoTracking ? query.AsNoTracking() : query;
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = GetRequiredSubject(claims);
        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private void CreateNotification(Guid userId, string title, string body, string? actionUrl, DateTime now) =>
        dbContext.UserNotifications.Add(new UserNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Category = NotificationCategories.DeveloperEnrollment,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            IsRead = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

    private static void AddConversationMessage(ConversationThread thread, AppUser author, string authorRole, string messageKind, string? body, IReadOnlyList<ConversationAttachmentDraft> attachments, DateTime now)
    {
        thread.Messages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            Thread = thread,
            UserId = author.Id,
            User = author,
            AuthorRole = authorRole,
            MessageKind = messageKind,
            Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Attachments = attachments.Select(attachment => new ConversationMessageAttachment
            {
                Id = Guid.NewGuid(),
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                Content = attachment.Content,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }).ToList()
        });
        thread.UpdatedAtUtc = now;
    }

    private static bool CanCreateNewRequest(DeveloperEnrollmentRequest request) =>
        request.Status == DeveloperEnrollmentStatuses.Cancelled ||
        (request.Status == DeveloperEnrollmentStatuses.Rejected && request.ReapplyAvailableAtUtc is not null && request.ReapplyAvailableAtUtc <= DateTime.UtcNow);

    private static bool IsOpenRequest(string status) =>
        status == DeveloperEnrollmentStatuses.PendingReview || status == DeveloperEnrollmentStatuses.AwaitingApplicantResponse;

    private static string GetActionRequiredBy(string status) =>
        status == DeveloperEnrollmentStatuses.PendingReview ? WorkflowActionRequiredBy.Moderator :
        status == DeveloperEnrollmentStatuses.AwaitingApplicantResponse ? WorkflowActionRequiredBy.Applicant :
        WorkflowActionRequiredBy.None;

    private static DeveloperEnrollmentStateSnapshot MapCurrentEnrollment(DeveloperEnrollmentRequest? request, bool developerAccessEnabledOverride)
    {
        if (developerAccessEnabledOverride)
        {
            return new(request?.Id, DeveloperEnrollmentStatuses.Approved, WorkflowActionRequiredBy.None, true, false, false, request?.RequestedAtUtc, request?.UpdatedAtUtc, request?.ReviewedAtUtc, request?.ReapplyAvailableAtUtc, request?.ReviewedByUser?.KeycloakSubject, false);
        }

        if (request is null)
        {
            return new(null, DeveloperEnrollmentStatuses.NotRequested, WorkflowActionRequiredBy.None, false, true, false, null, null, null, null, null, false);
        }

        var canSubmit = request.Status == DeveloperEnrollmentStatuses.Cancelled || (request.Status == DeveloperEnrollmentStatuses.Rejected && request.ReapplyAvailableAtUtc is not null && request.ReapplyAvailableAtUtc <= DateTime.UtcNow);
        return new(request.Id, request.Status, GetActionRequiredBy(request.Status), request.Status == DeveloperEnrollmentStatuses.Approved, canSubmit, request.Status == DeveloperEnrollmentStatuses.AwaitingApplicantResponse, request.RequestedAtUtc, request.UpdatedAtUtc, request.ReviewedAtUtc, request.ReapplyAvailableAtUtc, request.ReviewedByUser?.KeycloakSubject, IsOpenRequest(request.Status));
    }

    private static DeveloperEnrollmentRequestSnapshot MapRequest(DeveloperEnrollmentRequest request) =>
        new(request.Id, request.User.KeycloakSubject, request.User.DisplayName, request.User.Email, request.Status, GetActionRequiredBy(request.Status), request.Status == DeveloperEnrollmentStatuses.Approved, request.RequestedAtUtc, request.UpdatedAtUtc, request.ReviewedAtUtc, request.ReapplyAvailableAtUtc, request.ReviewedByUser?.KeycloakSubject);

    private static DeveloperEnrollmentConversationSnapshot MapConversation(DeveloperEnrollmentRequest request) =>
        new(
            request.Id,
            request.Status,
            GetActionRequiredBy(request.Status),
            request.ReviewedAtUtc,
            request.ReviewedByUser?.KeycloakSubject,
            request.ConversationThread.Messages.OrderBy(candidate => candidate.CreatedAtUtc).Select(candidate => new ConversationMessageSnapshot(candidate.Id, candidate.AuthorRole, candidate.User.KeycloakSubject, candidate.User.DisplayName, candidate.MessageKind, candidate.Body, candidate.CreatedAtUtc, candidate.Attachments.Select(attachment => new ConversationAttachmentSnapshot(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes)).ToArray())).ToArray());

    private static bool HasImmediateDeveloperAccess(IEnumerable<Claim> claims) => HasRole(claims, "developer") || HasRole(claims, "admin");
    private static bool HasModeratorAccess(IEnumerable<Claim> claims) => HasRole(claims, "moderator") || HasRole(claims, "admin");

    private static bool HasRole(IEnumerable<Claim> claims, string role) =>
        claims.Any(claim => claim.Type == ClaimTypes.Role && string.Equals(claim.Value, role, StringComparison.OrdinalIgnoreCase));

    private static string GetRequiredSubject(IEnumerable<Claim> claims)
    {
        var subject = ClaimValueResolver.GetSubject(claims);
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return subject;
    }
}

internal static class DeveloperEnrollmentStatuses
{
    public const string NotRequested = "not_requested";
    public const string Pending = PendingReview;
    public const string PendingReview = "pending_review";
    public const string AwaitingApplicantResponse = "awaiting_applicant_response";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

internal static class WorkflowActionRequiredBy
{
    public const string None = "none";
    public const string Applicant = "applicant";
    public const string Moderator = "moderator";
}

internal static class ConversationAuthorRoles
{
    public const string Applicant = "applicant";
    public const string Moderator = "moderator";
}

internal static class ConversationMessageKinds
{
    public const string ApplicantReply = "applicant_reply";
    public const string ModeratorInformationRequest = "moderator_information_request";
    public const string ModeratorRejection = "moderator_rejection";
}

internal static class NotificationCategories
{
    public const string DeveloperEnrollment = "developer_enrollment";
}

internal sealed record ConversationAttachmentDraft(string FileName, string ContentType, long SizeBytes, byte[] Content);
internal sealed record DeveloperEnrollmentStateSnapshot(Guid? RequestId, string Status, string ActionRequiredBy, bool DeveloperAccessEnabled, bool CanSubmitRequest, bool CanReply, DateTime? RequestedAtUtc, DateTime? UpdatedAtUtc, DateTime? ReviewedAtUtc, DateTime? ReapplyAvailableAtUtc, string? ReviewerSubject, bool CanCancelRequest);
internal sealed record DeveloperEnrollmentRequestSnapshot(Guid RequestId, string ApplicantSubject, string? ApplicantDisplayName, string? ApplicantEmail, string Status, string ActionRequiredBy, bool DeveloperAccessEnabled, DateTime RequestedAtUtc, DateTime UpdatedAtUtc, DateTime? ReviewedAtUtc, DateTime? ReapplyAvailableAtUtc, string? ReviewerSubject);
internal sealed record ConversationAttachmentSnapshot(Guid AttachmentId, string FileName, string ContentType, long SizeBytes);
internal sealed record ConversationMessageSnapshot(Guid MessageId, string AuthorRole, string AuthorSubject, string? AuthorDisplayName, string MessageKind, string? Body, DateTime CreatedAtUtc, IReadOnlyList<ConversationAttachmentSnapshot> Attachments);
internal sealed record DeveloperEnrollmentConversationSnapshot(Guid RequestId, string Status, string ActionRequiredBy, DateTime? ReviewedAtUtc, string? ReviewerSubject, IReadOnlyList<ConversationMessageSnapshot> Messages);
internal sealed record NotificationSnapshot(Guid NotificationId, string Category, string Title, string Body, string? ActionUrl, bool IsRead, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
internal sealed record AttachmentPayloadSnapshot(string FileName, string ContentType, byte[] Content);
internal sealed record DeveloperEnrollmentMutationResult(DeveloperEnrollmentMutationStatus Status, DeveloperEnrollmentStateSnapshot? Enrollment = null, string? ValidationCode = null);
internal sealed record DeveloperEnrollmentReviewResult(DeveloperEnrollmentReviewStatus Status, DeveloperEnrollmentRequestSnapshot? Request = null, string? ErrorDetail = null, string? ValidationCode = null);
internal sealed record DeveloperEnrollmentRequestListResult(DeveloperEnrollmentRequestListStatus Status, IReadOnlyList<DeveloperEnrollmentRequestSnapshot>? Requests = null);
internal sealed record DeveloperEnrollmentConversationResult(DeveloperEnrollmentConversationStatus Status, DeveloperEnrollmentConversationSnapshot? Conversation = null);
internal sealed record DeveloperEnrollmentAttachmentResult(DeveloperEnrollmentAttachmentStatus Status, AttachmentPayloadSnapshot? Attachment = null);
internal sealed record NotificationListResult(IReadOnlyList<NotificationSnapshot> Notifications);
internal sealed record NotificationMutationResult(NotificationMutationStatus Status, NotificationSnapshot? Notification = null);

internal enum DeveloperEnrollmentReviewDecision { Approve, Reject, RequestMoreInformation }
internal enum DeveloperEnrollmentMutationStatus { Success, NotFound, Conflict, Validation }
internal enum DeveloperEnrollmentRequestListStatus { Success, Forbidden }
internal enum DeveloperEnrollmentReviewStatus { Success, Forbidden, NotFound, Conflict, Validation, UpstreamFailure }
internal enum DeveloperEnrollmentConversationStatus { Success, Forbidden, NotFound }
internal enum DeveloperEnrollmentAttachmentStatus { Success, Forbidden, NotFound }
internal enum NotificationMutationStatus { Success, NotFound }
