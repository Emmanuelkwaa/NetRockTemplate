using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NetRockTemplate.WebApi.Features.OpenApi.Transformers;

/// <summary>
/// Sets the API title, version, description, and security scheme on the generated OpenAPI document.
/// </summary>
internal sealed class ProjectDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = "NetRockTemplate API";
        document.Info.Version = "v1";
        document.Info.Description = """
                                    Authentication is handled by Keycloak.

                                    ## Bearer Token Authentication (Mobile/API Clients)
                                    Obtain an access token from the Keycloak realm and send it as `Authorization: Bearer {accessToken}`.
                                    The token's audience must include this API's client ID.

                                    ## Cookie Authentication (Web Clients)
                                    1. Navigate the browser to `GET /api/auth/keycloak/login?returnUrl=/`
                                    2. After sign-in, the callback sets `__Secure-ACCESS-TOKEN` and `__Secure-REFRESH-TOKEN` as HttpOnly cookies
                                    3. Call `POST /api/auth/refresh?useCookies=true` to rotate them and `POST /api/auth/logout` to sign out
                                    """;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["bearerAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Keycloak access token: Authorization: Bearer {token}"
        };

        return Task.CompletedTask;
    }
}
