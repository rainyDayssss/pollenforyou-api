using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PollenForYouApi.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration, string policyName)
    {
        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, policy =>
            {
                policy.AllowAnyHeader().AllowAnyMethod();
                if (corsOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOrigins);
                }
            });
        });
        return services;
    }
}