using JetBrains.Annotations;

namespace NetRockTemplate.WebApi.Features.Users.Dtos;

/// <summary>
/// The current user's identity from Keycloak. Profile data is managed in Keycloak.
/// </summary>
public class UserResponse
{
    /// <summary>
    /// The Keycloak user ID (<c>sub</c>).
    /// </summary>
    public Guid Id { [UsedImplicitly] get; init; }

    /// <summary>
    /// The username (<c>preferred_username</c>).
    /// </summary>
    public string Username { [UsedImplicitly] get; init; } = string.Empty;

    /// <summary>
    /// The email address.
    /// </summary>
    public string Email { [UsedImplicitly] get; init; } = string.Empty;

    /// <summary>
    /// The given name, when the token carries one.
    /// </summary>
    public string? FirstName { [UsedImplicitly] get; init; }

    /// <summary>
    /// The family name, when the token carries one.
    /// </summary>
    public string? LastName { [UsedImplicitly] get; init; }
}
