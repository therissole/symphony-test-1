using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SymphonyTest1.Api.Infrastructure.Authentication;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException(
                "Authentication:Authority must identify the Keycloak realm.");
        var audience = configuration["Authentication:Audience"]
            ?? throw new InvalidOperationException(
                "Authentication:Audience must identify this API.");
        var requireHttpsMetadata = configuration.GetValue(
            "Authentication:RequireHttpsMetadata",
            true);
        var metadataAddress = configuration["Authentication:MetadataAddress"];

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException(
                "Authentication:Authority must be an absolute URI.");
        }

        if (requireHttpsMetadata && authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Authentication:Authority must use HTTPS when metadata HTTPS is required.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                if (!string.IsNullOrWhiteSpace(metadataAddress))
                {
                    options.MetadataAddress = metadataAddress;
                }
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
