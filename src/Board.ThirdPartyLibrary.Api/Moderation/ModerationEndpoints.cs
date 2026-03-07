using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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

        group.MapGet("/developers", [Authorize] async (
            ClaimsPrincipal user,
            BoardLibraryDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (!HasModeratorAccess(user.Claims))
            {
                return CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators and above can view developer moderation tools.",
                    "moderator_access_required");
            }

            var developers = await dbContext.Users
                .AsNoTracking()
                .OrderBy(candidate => candidate.UserName ?? candidate.DisplayName ?? candidate.Email ?? candidate.KeycloakSubject)
                .Select(candidate => new ModerationDeveloperSummary(
                    candidate.KeycloakSubject,
                    candidate.UserName,
                    candidate.DisplayName,
                    candidate.Email))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(new ModerationDeveloperListResponse(developers));
        });

        group.MapGet("/developers/{developerIdentifier}/verification", [Authorize] async (
            ClaimsPrincipal user,
            string developerIdentifier,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.GetVerifiedDeveloperRoleStateAsync(
                user.Claims,
                developerIdentifier,
                cancellationToken);

            return result.Status switch
            {
                VerifiedDeveloperRoleReadStatus.Success => Results.Ok(
                    new VerifiedDeveloperRoleStateResponse(MapVerifiedDeveloperRoleState(result.State!))),
                VerifiedDeveloperRoleReadStatus.Forbidden => CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators and above can manage verification state.",
                    "moderator_access_required"),
                VerifiedDeveloperRoleReadStatus.NotFound => Results.NotFound(),
                VerifiedDeveloperRoleReadStatus.UpstreamFailure => CreateProblemResult(
                    StatusCodes.Status502BadGateway,
                    "Verification lookup failed.",
                    result.ErrorDetail ?? "Keycloak role lookup failed for the target user.",
                    "keycloak_verified_developer_role_failed"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapPut("/developers/{developerSubject}/verified-developer", [Authorize] async (
            ClaimsPrincipal user,
            string developerSubject,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.SetVerifiedDeveloperRoleAsync(
                user.Claims,
                developerSubject,
                verifiedDeveloper: true,
                cancellationToken);

            return MapVerifiedDeveloperRoleMutationResult(result);
        });

        group.MapDelete("/developers/{developerSubject}/verified-developer", [Authorize] async (
            ClaimsPrincipal user,
            string developerSubject,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.SetVerifiedDeveloperRoleAsync(
                user.Claims,
                developerSubject,
                verifiedDeveloper: false,
                cancellationToken);

            return MapVerifiedDeveloperRoleMutationResult(result);
        });

        return app;
    }

    private static IResult MapVerifiedDeveloperRoleMutationResult(VerifiedDeveloperRoleMutationResult result) =>
        result.Status switch
        {
            VerifiedDeveloperRoleMutationStatus.Success => Results.Ok(
                new VerifiedDeveloperRoleStateResponse(MapVerifiedDeveloperRoleState(result.State!))),
            VerifiedDeveloperRoleMutationStatus.Forbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Moderator access is required.",
                "Only moderators and above can manage verified developer role assignments.",
                "moderator_access_required"),
            VerifiedDeveloperRoleMutationStatus.NotFound => Results.NotFound(),
            VerifiedDeveloperRoleMutationStatus.UpstreamFailure => CreateProblemResult(
                StatusCodes.Status502BadGateway,
                "Verified developer role update failed.",
                result.ErrorDetail ?? "Keycloak role assignment failed for the target user.",
                "keycloak_verified_developer_role_failed"),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static VerifiedDeveloperRoleState MapVerifiedDeveloperRoleState(VerifiedDeveloperRoleStateSnapshot snapshot) =>
        new(snapshot.DeveloperSubject, snapshot.VerifiedDeveloper, snapshot.AlreadyInRequestedState);

    private static bool HasModeratorAccess(IEnumerable<Claim> claims) =>
        claims.Any(claim =>
            claim.Type == ClaimTypes.Role &&
            (string.Equals(claim.Value, "moderator", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Value, "admin", StringComparison.OrdinalIgnoreCase)));

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
/// Moderation-visible user summary for developer verification workflows.
/// </summary>
/// <param name="DeveloperSubject">Stable Keycloak subject identifier.</param>
/// <param name="UserName">Cached username when available.</param>
/// <param name="DisplayName">Cached display name when available.</param>
/// <param name="Email">Cached email when available.</param>
internal sealed record ModerationDeveloperSummary(
    string DeveloperSubject,
    string? UserName,
    string? DisplayName,
    string? Email);

/// <summary>
/// Response wrapper for moderation developer listing.
/// </summary>
/// <param name="Developers">Returned moderation user list.</param>
internal sealed record ModerationDeveloperListResponse(IReadOnlyList<ModerationDeveloperSummary> Developers);

/// <summary>
/// Verified developer role state returned by moderation role-mutation endpoints.
/// </summary>
/// <param name="DeveloperSubject">Target developer Keycloak subject.</param>
/// <param name="VerifiedDeveloper">Whether verified developer role is currently assigned.</param>
/// <param name="AlreadyInRequestedState">Whether the role already matched the requested state before this operation.</param>
internal sealed record VerifiedDeveloperRoleState(
    string DeveloperSubject,
    bool VerifiedDeveloper,
    bool AlreadyInRequestedState);

/// <summary>
/// Response wrapper for verified developer role mutations.
/// </summary>
/// <param name="VerifiedDeveloperRoleState">Returned verified role state.</param>
internal sealed record VerifiedDeveloperRoleStateResponse(VerifiedDeveloperRoleState VerifiedDeveloperRoleState);

/// <summary>
/// Problem-details envelope used by moderation endpoints.
/// </summary>
internal sealed record ModerationProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
