using JetBrains.Annotations;

namespace NetRockTemplate.WebApi.Features.Authentication.Dtos;

/// <summary>
/// Request carrying a Keycloak refresh token. Optional for web clients, which use the cookie.
/// </summary>
public class RefreshRequest
{
    /// <summary>
    /// The refresh token issued by Keycloak.
    /// </summary>
    public string? RefreshToken { [UsedImplicitly] get; [UsedImplicitly] init; }
}
