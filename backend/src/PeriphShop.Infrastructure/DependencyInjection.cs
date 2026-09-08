using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeriphShop.Application.Abstractions;
using PeriphShop.Infrastructure.Auditing;
using PeriphShop.Infrastructure.Notifications;
using PeriphShop.Infrastructure.Persistence;
using PeriphShop.Infrastructure.Security;

namespace PeriphShop.Infrastructure;

/// <summary>Регистрация инфраструктуры: БД, безопасность, почта, аудит.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("Не задана строка подключения ConnectionStrings:Default");

        services.AddDbContext<AppDbContext>(options =>
        {
            // Версия сервера задаётся явно: автоопределение потребовало бы доступной БД
            // в момент старта и на этапе создания миграций.
            var serverVersion = new MySqlServerVersion(new Version(8, 4, 0));

            options.UseMySql(connectionString, serverVersion, mysql =>
            {
                mysql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // NFR-04: повтор транзакции при взаимоблокировке или обрыве соединения.
                mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            });

            if (configuration.GetValue("Database:DetailedErrors", false))
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DbSeeder>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuditService, AuditService>();

        var smtpEnabled = configuration.GetValue($"{SmtpOptions.SectionName}:Enabled", true);
        if (smtpEnabled) services.AddScoped<IEmailSender, SmtpEmailSender>();
        else services.AddScoped<IEmailSender, NullEmailSender>();

        return services;
    }
}
