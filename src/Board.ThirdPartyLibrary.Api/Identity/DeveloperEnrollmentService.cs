using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Identity;

internal interface IDeveloperEnrollmentService
{
    Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<DeveloperEnrollmentMutationResult> SubmitEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
    Task<VerifiedDeveloperRoleReadResult> GetVerifiedDeveloperRoleStateAsync(IEnumerable<Claim> claims, string developerIdentifier, CancellationToken cancellationToken = default);
    Task<VerifiedDeveloperRoleMutationResult> SetVerifiedDeveloperRoleAsync(IEnumerable<Claim> claims, string developerIdentifier, bool verifiedDeveloper, CancellationToken cancellationToken = default);
    Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
}

internal sealed class DeveloperEnrollmentService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService,
    IKeycloakUserRoleClient keycloakUserRoleClient) : IDeveloperEnrollmentService
{
    private const string DeveloperRoleName = "developer";
    private const string VerifiedDeveloperRoleName = "verified_developer";

    public async Task<DeveloperEnrollmentStateSnapshot> GetCurrentEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var hasDeveloperAccess = HasDeveloperRole(claims);
        var hasVerifiedDeveloperRole = hasDeveloperAccess && HasRole(claims, VerifiedDeveloperRoleName);

        if (!hasDeveloperAccess)
        {
            var subject = GetRequiredSubject(claims);
            var developerRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(subject, DeveloperRoleName, cancellationToken);
            if (developerRoleCheck.Succeeded && developerRoleCheck.IsAssigned)
            {
                hasDeveloperAccess = true;
                var verifiedRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(subject, VerifiedDeveloperRoleName, cancellationToken);
                if (verifiedRoleCheck.Succeeded)
                {
                    hasVerifiedDeveloperRole = verifiedRoleCheck.IsAssigned;
                }
            }
        }

        return CreateEnrollmentStateSnapshot(hasDeveloperAccess, hasVerifiedDeveloperRole);
    }

    public async Task<DeveloperEnrollmentMutationResult> SubmitEnrollmentAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var hasDeveloperAccess = HasDeveloperRole(claims);
        var hasVerifiedDeveloperRole = hasDeveloperAccess && HasRole(claims, VerifiedDeveloperRoleName);
        if (!hasDeveloperAccess)
        {
            var subject = GetRequiredSubject(claims);
            var assignmentResult = await keycloakUserRoleClient.EnsureRealmRoleAssignedAsync(subject, DeveloperRoleName, cancellationToken);
            if (!assignmentResult.Succeeded)
            {
                return new(DeveloperEnrollmentMutationStatus.UpstreamFailure, ErrorDetail: assignmentResult.ErrorDetail ?? "Keycloak role assignment failed for the authenticated user.");
            }

            hasDeveloperAccess = true;
            var verifiedRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(subject, VerifiedDeveloperRoleName, cancellationToken);
            if (verifiedRoleCheck.Succeeded)
            {
                hasVerifiedDeveloperRole = verifiedRoleCheck.IsAssigned;
            }
        }

        return new(DeveloperEnrollmentMutationStatus.Success, CreateEnrollmentStateSnapshot(hasDeveloperAccess, hasVerifiedDeveloperRole));
    }

    public async Task<VerifiedDeveloperRoleReadResult> GetVerifiedDeveloperRoleStateAsync(
        IEnumerable<Claim> claims,
        string developerIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new(VerifiedDeveloperRoleReadStatus.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(developerIdentifier))
        {
            return new(VerifiedDeveloperRoleReadStatus.NotFound);
        }

        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var normalizedIdentifier = developerIdentifier.Trim();
        var resolvedSubject = await ResolveDeveloperSubjectAsync(normalizedIdentifier, cancellationToken);

        var developerRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(resolvedSubject, DeveloperRoleName, cancellationToken);
        if (developerRoleCheck.UserNotFound || (developerRoleCheck.Succeeded && !developerRoleCheck.IsAssigned))
        {
            return new(VerifiedDeveloperRoleReadStatus.NotFound);
        }

        if (!developerRoleCheck.Succeeded)
        {
            return new(
                VerifiedDeveloperRoleReadStatus.UpstreamFailure,
                ErrorDetail: developerRoleCheck.ErrorDetail ?? "Keycloak developer-role lookup failed for the target user.");
        }

        var verifiedRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(resolvedSubject, VerifiedDeveloperRoleName, cancellationToken);
        if (!verifiedRoleCheck.Succeeded)
        {
            return new(
                VerifiedDeveloperRoleReadStatus.UpstreamFailure,
                ErrorDetail: verifiedRoleCheck.ErrorDetail ?? "Keycloak verified-developer role lookup failed for the target user.");
        }

        return new(
            VerifiedDeveloperRoleReadStatus.Success,
            State: new VerifiedDeveloperRoleStateSnapshot(resolvedSubject, verifiedRoleCheck.IsAssigned, false));
    }

    public async Task<VerifiedDeveloperRoleMutationResult> SetVerifiedDeveloperRoleAsync(
        IEnumerable<Claim> claims,
        string developerIdentifier,
        bool verifiedDeveloper,
        CancellationToken cancellationToken = default)
    {
        if (!HasModeratorAccess(claims))
        {
            return new(VerifiedDeveloperRoleMutationStatus.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(developerIdentifier))
        {
            return new(VerifiedDeveloperRoleMutationStatus.NotFound);
        }

        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var normalizedIdentifier = developerIdentifier.Trim();
        var resolvedSubject = await ResolveDeveloperSubjectAsync(normalizedIdentifier, cancellationToken);

        var developerRoleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(resolvedSubject, DeveloperRoleName, cancellationToken);
        if (developerRoleCheck.UserNotFound || (developerRoleCheck.Succeeded && !developerRoleCheck.IsAssigned))
        {
            return new(VerifiedDeveloperRoleMutationStatus.NotFound);
        }

        if (!developerRoleCheck.Succeeded)
        {
            return new(
                VerifiedDeveloperRoleMutationStatus.UpstreamFailure,
                ErrorDetail: developerRoleCheck.ErrorDetail ?? "Keycloak developer-role lookup failed for the target user.");
        }

        var mutationResult = verifiedDeveloper
            ? await keycloakUserRoleClient.EnsureRealmRoleAssignedAsync(resolvedSubject, VerifiedDeveloperRoleName, cancellationToken)
            : await keycloakUserRoleClient.EnsureRealmRoleRemovedAsync(resolvedSubject, VerifiedDeveloperRoleName, cancellationToken);

        if (mutationResult.UserNotFound)
        {
            return new(VerifiedDeveloperRoleMutationStatus.NotFound);
        }

        if (!mutationResult.Succeeded)
        {
            return new(
                VerifiedDeveloperRoleMutationStatus.UpstreamFailure,
                ErrorDetail: mutationResult.ErrorDetail ?? "Keycloak verified-developer role update failed for the target user.");
        }

        return new(
            VerifiedDeveloperRoleMutationStatus.Success,
            State: new VerifiedDeveloperRoleStateSnapshot(resolvedSubject, verifiedDeveloper, mutationResult.AlreadyInRequestedState));
    }

    public async Task<bool> HasDeveloperAccessAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        if (HasDeveloperRole(claims))
        {
            return true;
        }

        var subject = GetRequiredSubject(claims);
        var roleCheck = await keycloakUserRoleClient.IsRealmRoleAssignedAsync(subject, DeveloperRoleName, cancellationToken);
        return roleCheck.Succeeded && roleCheck.IsAssigned;
    }

    private static DeveloperEnrollmentStateSnapshot CreateEnrollmentStateSnapshot(bool developerAccessEnabled, bool verifiedDeveloper) =>
        developerAccessEnabled
            ? new(DeveloperEnrollmentStatuses.Enrolled, WorkflowActionRequiredBy.None, true, verifiedDeveloper, false)
            : new(DeveloperEnrollmentStatuses.NotEnrolled, WorkflowActionRequiredBy.None, false, false, true);

    private static bool HasDeveloperRole(IEnumerable<Claim> claims) =>
        HasRole(claims, DeveloperRoleName) || HasRole(claims, "admin");

    private static bool HasModeratorAccess(IEnumerable<Claim> claims) =>
        HasRole(claims, "moderator") || HasRole(claims, "admin");

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

    private async Task<string> ResolveDeveloperSubjectAsync(string developerIdentifier, CancellationToken cancellationToken)
    {
        var normalizedIdentifier = developerIdentifier.Trim();
        var normalizedIdentifierLower = normalizedIdentifier.ToLowerInvariant();
        var matchedUser = await dbContext.Users
            .AsNoTracking()
            .Where(candidate =>
                candidate.KeycloakSubject == normalizedIdentifier ||
                (candidate.UserName != null && candidate.UserName.ToLower() == normalizedIdentifierLower))
            .Select(candidate => candidate.KeycloakSubject)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(matchedUser)
            ? normalizedIdentifier
            : matchedUser;
    }
}

internal static class DeveloperEnrollmentStatuses
{
    public const string NotEnrolled = "not_enrolled";
    public const string Enrolled = "enrolled";
}

internal static class WorkflowActionRequiredBy
{
    public const string None = "none";
}

internal sealed record DeveloperEnrollmentStateSnapshot(
    string Status,
    string ActionRequiredBy,
    bool DeveloperAccessEnabled,
    bool VerifiedDeveloper,
    bool CanSubmitRequest);

internal sealed record DeveloperEnrollmentMutationResult(
    DeveloperEnrollmentMutationStatus Status,
    DeveloperEnrollmentStateSnapshot? Enrollment = null,
    string? ErrorDetail = null);

internal sealed record VerifiedDeveloperRoleStateSnapshot(
    string DeveloperSubject,
    bool VerifiedDeveloper,
    bool AlreadyInRequestedState);

internal sealed record VerifiedDeveloperRoleMutationResult(
    VerifiedDeveloperRoleMutationStatus Status,
    VerifiedDeveloperRoleStateSnapshot? State = null,
    string? ErrorDetail = null);

internal sealed record VerifiedDeveloperRoleReadResult(
    VerifiedDeveloperRoleReadStatus Status,
    VerifiedDeveloperRoleStateSnapshot? State = null,
    string? ErrorDetail = null);

internal enum DeveloperEnrollmentMutationStatus
{
    Success,
    UpstreamFailure
}

internal enum VerifiedDeveloperRoleMutationStatus
{
    Success,
    Forbidden,
    NotFound,
    UpstreamFailure
}

internal enum VerifiedDeveloperRoleReadStatus
{
    Success,
    Forbidden,
    NotFound,
    UpstreamFailure
}
