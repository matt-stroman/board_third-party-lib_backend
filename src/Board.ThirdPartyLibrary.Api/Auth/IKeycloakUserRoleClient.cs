namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Assigns realm roles to Keycloak users.
/// </summary>
internal interface IKeycloakUserRoleClient
{
    /// <summary>
    /// Ensures the supplied realm role is assigned to the supplied Keycloak user.
    /// </summary>
    /// <param name="userSubject">Keycloak user subject identifier.</param>
    /// <param name="roleName">Realm role name to ensure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role-assignment result.</returns>
    Task<KeycloakUserRoleMutationResult> EnsureRealmRoleAssignedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the supplied realm role is removed from the supplied Keycloak user.
    /// </summary>
    /// <param name="userSubject">Keycloak user subject identifier.</param>
    /// <param name="roleName">Realm role name to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role-removal result.</returns>
    Task<KeycloakUserRoleMutationResult> EnsureRealmRoleRemovedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves whether the supplied Keycloak user currently has the supplied realm role.
    /// </summary>
    /// <param name="userSubject">Keycloak user subject identifier.</param>
    /// <param name="roleName">Realm role name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The role-check result.</returns>
    Task<KeycloakUserRoleCheckResult> IsRealmRoleAssignedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result returned by <see cref="IKeycloakUserRoleClient"/>.
/// </summary>
/// <param name="Succeeded">Whether the role mutation operation succeeded.</param>
/// <param name="AlreadyInRequestedState">Whether the role was already in the requested state before the operation.</param>
/// <param name="UserNotFound">Whether the target Keycloak user was not found.</param>
/// <param name="ErrorDetail">Optional upstream failure detail.</param>
internal sealed record KeycloakUserRoleMutationResult(
    bool Succeeded,
    bool AlreadyInRequestedState,
    bool UserNotFound,
    string? ErrorDetail)
{
    /// <summary>
    /// Creates a successful role-mutation result.
    /// </summary>
    /// <param name="alreadyInRequestedState">Whether the role was already in the requested state.</param>
    /// <returns>The success result.</returns>
    public static KeycloakUserRoleMutationResult Success(bool alreadyInRequestedState) =>
        new(true, alreadyInRequestedState, false, null);

    /// <summary>
    /// Creates a target-user-not-found mutation result.
    /// </summary>
    /// <returns>The not-found result.</returns>
    public static KeycloakUserRoleMutationResult NotFound() =>
        new(false, false, true, null);

    /// <summary>
    /// Creates a failed role-mutation result.
    /// </summary>
    /// <param name="errorDetail">Failure detail.</param>
    /// <returns>The failed result.</returns>
    public static KeycloakUserRoleMutationResult Failure(string errorDetail) =>
        new(false, false, false, errorDetail);
}

/// <summary>
/// Result returned when checking whether a user has a realm role.
/// </summary>
/// <param name="Succeeded">Whether the check succeeded.</param>
/// <param name="IsAssigned">Whether the role is currently assigned.</param>
/// <param name="UserNotFound">Whether the target Keycloak user was not found.</param>
/// <param name="ErrorDetail">Optional upstream failure detail.</param>
internal sealed record KeycloakUserRoleCheckResult(
    bool Succeeded,
    bool IsAssigned,
    bool UserNotFound,
    string? ErrorDetail)
{
    /// <summary>
    /// Creates a successful role-check result.
    /// </summary>
    /// <param name="isAssigned">Whether the role is assigned.</param>
    /// <returns>The success result.</returns>
    public static KeycloakUserRoleCheckResult Success(bool isAssigned) =>
        new(true, isAssigned, false, null);

    /// <summary>
    /// Creates a target-user-not-found role-check result.
    /// </summary>
    /// <returns>The not-found result.</returns>
    public static KeycloakUserRoleCheckResult NotFound() =>
        new(false, false, true, null);

    /// <summary>
    /// Creates a failed role-check result.
    /// </summary>
    /// <param name="errorDetail">Failure detail.</param>
    /// <returns>The failed result.</returns>
    public static KeycloakUserRoleCheckResult Failure(string errorDetail) =>
        new(false, false, false, errorDetail);
}
