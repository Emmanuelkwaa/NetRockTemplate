using Microsoft.AspNetCore.Http;
using NetRockTemplate.Application.Identity;

namespace NetRockTemplate.Infrastructure.Identity;

/// <summary>
/// Reads the caller's identity from the validated Keycloak access token.
/// Inbound claim mapping is off, so claims keep their JWT names (<c>sub</c>, <c>email</c>, <c>preferred_username</c>).
/// </summary>
internal class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <inheritdoc />
    public Guid? UserId => Guid.TryParse(GetClaimValue("sub"), out var userId) ? userId : null;

    /// <inheritdoc />
    public string? Email => GetClaimValue("email");

    /// <inheritdoc />
    public string? UserName => GetClaimValue("preferred_username");

    /// <inheritdoc />
    public Guid AuthenticatedUserId =>
        UserId ?? throw new InvalidOperationException("No authenticated user. This property should only be accessed on authenticated endpoints.");

    /// <inheritdoc />
    public bool IsAuthenticated => UserId.HasValue;

    private string? GetClaimValue(string claimType) =>
        httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;
}
