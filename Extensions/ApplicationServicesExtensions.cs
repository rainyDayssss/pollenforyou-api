using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PollenForYouApi.Filters;
using PollenForYouApi.Profiles;
using PollenForYouApi.Repositories;
using PollenForYouApi.Services;
using PollenForYouApi.Validators;

namespace PollenForYouApi.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(UserMappingProfile).Assembly));
        services.AddValidatorsFromAssembly(typeof(CreateUserRequestValidator).Assembly);
        services.AddScoped<ValidationFilter>();
        
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();
        
        return services;
    }
}