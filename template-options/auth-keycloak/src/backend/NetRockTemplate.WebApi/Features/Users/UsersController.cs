using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetRockTemplate.Application.Identity;
using NetRockTemplate.WebApi.Features.Users.Dtos;

namespace NetRockTemplate.WebApi.Features.Users;

/// <summary>
/// The current user, as described by the Keycloak access token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Users")]
public class UsersController(IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Gets the current user from the token claims.
    /// </summary>
    /// <response code="200">The current user.</response>
    /// <response code="401">The request is not authenticated.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<UserResponse> GetCurrentUser() => Ok(new UserResponse
    {
        Id = userContext.AuthenticatedUserId,
        Username = userContext.UserName ?? string.Empty,
        Email = userContext.Email ?? string.Empty,
        FirstName = User.FindFirst("given_name")?.Value,
        LastName = User.FindFirst("family_name")?.Value
    });
}
