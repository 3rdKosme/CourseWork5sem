using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PeriphShop.Application.Services;

namespace PeriphShop.Application;

/// <summary>Регистрация прикладного слоя в контейнере DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<OrderService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<PromoService>();
        services.AddScoped<FavoriteService>();
        services.AddScoped<AdminCatalogService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<ReportService>();
        services.AddScoped<UserAdminService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        return services;
    }
}
