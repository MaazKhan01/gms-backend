using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace API.Configurations.OpenApi;

/// <summary>
/// Adds the JWT Bearer security scheme to the generated OpenAPI document so the
/// "Authorize" affordance works in Scalar (replaces the old Swashbuckle
/// AddSecurityDefinition / AddSecurityRequirement wiring).
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var bearerScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Authorization header. Enter your token (Scalar adds the 'Bearer ' prefix).",
            Reference = new OpenApiReference
            {
                Id = "Bearer",
                Type = ReferenceType.SecurityScheme
            }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = bearerScheme;

        document.SecurityRequirements ??= new List<OpenApiSecurityRequirement>();
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [bearerScheme] = Array.Empty<string>()
        });

        return Task.CompletedTask;
    }
}
