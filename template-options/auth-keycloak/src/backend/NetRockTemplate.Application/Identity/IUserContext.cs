namespace NetRockTemplate.Application.Identity;

/// <summary>
/// Provides the identity of the current caller, read from the claims of the validated Keycloak access token.
/// </summary>
public interface IUserContext
{
    /// <summary>
    /// Gets the Keycloak user ID (<c>sub</c> claim), or <c>null</c> when the request is anonymous.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets the email address (<c>email</c> claim), or <c>null</c> when absent.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the username (<c>preferred_username</c> claim), or <c>null</c> when absent.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Gets the user ID of the authenticated caller.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the request is anonymous.</exception>
    Guid AuthenticatedUserId { get; }

    /// <summary>
    /// Gets a value indicating whether the caller is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
