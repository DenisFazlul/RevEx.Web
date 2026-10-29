using RevEx.Domain.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.IdentityModel.Tokens;
using RevEx.Api.Authorization;

namespace RevEx.Api.Extensions;

public static class AuthenticationServiceCollectionExtensions
{
    private const string Keycloak = "Keycloak";
    private const string ActiveDirectory = "ActiveDirectory";

    public static IServiceCollection AddConfiguredAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authenticationSection = configuration.GetRequiredSection("Authentication");
        var provider = authenticationSection["Provider"]
            ?? throw new InvalidOperationException("Authentication:Provider is required.");

        if (provider.Equals(Keycloak, StringComparison.OrdinalIgnoreCase))
            AddKeycloakAuthentication(services, authenticationSection);
        else if (provider.Equals(ActiveDirectory, StringComparison.OrdinalIgnoreCase))
            services.AddAuthentication(IISDefaults.AuthenticationScheme);
        else
            throw new InvalidOperationException(
                $"Authentication provider '{provider}' is not supported. " +
                $"Supported providers: {Keycloak}, {ActiveDirectory}.");

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(RevExRoles.User, RevExRoles.Moderator, RevExRoles.Administrator)
                .Build();
            options.AddPolicy(RevExPolicies.EditContent, policy => policy
                .RequireRole(RevExRoles.Moderator, RevExRoles.Administrator));
            options.AddPolicy(RevExPolicies.AdministerCatalog, policy => policy
                .RequireRole(RevExRoles.Administrator));
        });

        return services;
    }

    private static void AddKeycloakAuthentication(
        IServiceCollection services,
        IConfigurationSection authenticationSection)
    {
        var providerSection = authenticationSection.GetRequiredSection("Providers:Keycloak");
        var authority = providerSection["Authority"]
            ?? throw new InvalidOperationException(
                "Authentication:Providers:Keycloak:Authority is required.");
        var audience = providerSection["Audience"]
            ?? throw new InvalidOperationException(
                "Authentication:Providers:Keycloak:Audience is required.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = authority;
                options.Audience = audience;

                var metadataAddress = providerSection["MetadataAddress"];
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                    options.MetadataAddress = metadataAddress;

                options.RequireHttpsMetadata = providerSection.GetValue(
                    "RequireHttpsMetadata",
                    true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    RoleClaimType = "roles"
                };
            });
    }
}
