// @init:if auth=jwt,keycloak
using Microsoft.AspNetCore.Authorization;
// @init:end
using Microsoft.AspNetCore.Mvc;

namespace NetRockTemplate.WebApi.Shared;

/// <summary>
// @init:if auth=jwt,keycloak
/// Abstract base controller for all authorized, versioned API endpoints.
/// Provides <c>[ApiController]</c>, <c>[Authorize]</c>, and the <c>api/v1/[controller]</c> route prefix.
// @init:else
/// Abstract base controller for all versioned API endpoints.
/// Provides <c>[ApiController]</c> and the <c>api/v1/[controller]</c> route prefix.
// @init:end
/// Error status codes map to <see cref="ProblemDetails"/> via ASP.NET Core's client error mapping.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
// @init:if auth=jwt,keycloak
[Authorize]
// @init:end
public abstract class ApiController : ControllerBase;
