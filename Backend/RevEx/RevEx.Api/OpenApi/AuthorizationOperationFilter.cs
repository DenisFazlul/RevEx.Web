using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RevEx.Api.OpenApi;

public sealed class AuthorizationOperationFilter(IConfiguration configuration) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        operation.OperationId = $"{context.MethodInfo.DeclaringType!.Name.Replace("Controller", "")}_{context.MethodInfo.Name}";
        foreach (var response in operation.Responses.Where(x => x.Key is "200" or "206").Select(x => x.Value))
        {
            if (context.ApiDescription.SupportedResponseTypes.Any(x =>
                x.ApiResponseFormats.Any(format => format.MediaType == "application/octet-stream")))
                response.Content["application/octet-stream"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema { Type = "string", Format = "binary" }
                };
        }
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security = [];
            return;
        }

        var policies = metadata.OfType<IAuthorizeData>().Select(x => x.Policy)
            .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
        operation.Extensions["x-authorization-policies"] = new OpenApiArray();
        var extension = (OpenApiArray)operation.Extensions["x-authorization-policies"];
        foreach (var policy in policies) extension.Add(new OpenApiString(policy));
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Требуется аутентификация." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Недостаточно прав." });
        if (configuration["Authentication:Provider"]?.Equals("Keycloak", StringComparison.OrdinalIgnoreCase) == true)
        {
            // The scheme is supplied by AddApiSwagger for the configured Keycloak provider.
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = []
            }];
        }
    }
}
