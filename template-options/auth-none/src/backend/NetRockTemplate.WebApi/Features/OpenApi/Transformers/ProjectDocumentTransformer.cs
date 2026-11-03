using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NetRockTemplate.WebApi.Features.OpenApi.Transformers;

/// <summary>
/// Sets the API title, version, and description on the generated OpenAPI document.
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
        document.Info.Description = "This API has no authentication. Protect it at the network or gateway level before exposing it publicly.";

        return Task.CompletedTask;
    }
}
