using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.OpenApi.Models;

namespace RevEx.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers(options => options.Filters.Add<RevEx.Api.Filters.ContentOperationExceptionFilter>())
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                    var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState,
                        detail: "Проверьте обязательные поля и формат введённых данных.");
                    problem.Extensions["kind"] = "controlled";
                    problem.Extensions["code"] = "request.invalid";
                    return new BadRequestObjectResult(problem)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });
        services.AddHttpContextAccessor();
        services.AddScoped<RevEx.Application.Catalog.ContentFilesService>();
        services.AddScoped<RevEx.Application.Subscriptions.ContentSubscriptionService>();
        services.AddScoped<RevEx.Application.Abstractions.Identity.ICurrentUser, RevEx.Api.Authorization.HttpCurrentUser>();
        services.AddScoped<RevEx.Application.Content.ContentService>();
        services.AddScoped<RevEx.Application.Content.ContentParameterService>();
        services.AddScoped<RevEx.Application.Catalog.CategoriesService>();
        services.AddScoped<RevEx.Application.Catalog.ContentStatusesService>();
        services.AddScoped<RevEx.Application.Catalog.TagsService>();
        services.AddScoped<RevEx.Application.Catalog.TagGroupsService>();
        services.AddScoped<RevEx.Application.Catalog.ContentVersionsService>();
        services.AddScoped<RevEx.Application.Catalog.VersionImportsService>();
        services.AddScoped<RevEx.Application.Catalog.ContentHistoryService>();
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
