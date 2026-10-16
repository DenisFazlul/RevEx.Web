using Microsoft.OpenApi.Models;

namespace RevEx.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers();
        return services;
    }

    public static IServiceCollection AddApiSwagger(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "RevEx Web API",
                Version = "v1",
                Description = "API каталога RevEx, файлов, истории и управления релизами desktop. " +
                    "По умолчанию требуется авторизация. Политика EditContent: moderator/administrator; " +
                    "AdministerCatalog: administrator."
            });
            options.OperationFilter<RevEx.Api.OpenApi.AuthorizationOperationFilter>();
            if (!IsKeycloakConfigured(configuration))
                return;

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

        });

        return services;
    }

    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy => policy
                .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        return services;
    }

    private static bool IsKeycloakConfigured(IConfiguration configuration) =>
        configuration["Authentication:Provider"]?.Equals(
            "Keycloak",
            StringComparison.OrdinalIgnoreCase) == true;
}
