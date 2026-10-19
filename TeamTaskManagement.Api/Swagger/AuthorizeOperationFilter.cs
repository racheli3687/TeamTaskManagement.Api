// TeamTaskManagement.Api/Swagger/AuthorizeOperationFilter.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TeamTaskManagement.Api.Swagger;

// מסמן מנעול (דרישת Bearer) רק על endpoints שדורשים token.
// [Authorize] ברמת ה-Controller או ה-Action נכלל ב-EndpointMetadata, וכך גם [AllowAnonymous].
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public const string SchemeId = "Bearer";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization)
        {
            return;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = SchemeId
                }
            }] = Array.Empty<string>()
        });
    }
}