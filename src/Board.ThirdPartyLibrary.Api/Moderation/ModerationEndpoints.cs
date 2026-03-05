using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authorization;

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
