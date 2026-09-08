using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PeriphShop.Infrastructure.Persistence;

/// <summary>
/// Фабрика контекста для инструментов EF Core (dotnet ef migrations add / script).
/// Строка подключения берётся из переменной окружения PERIPHSHOP_CONNECTION либо значения по умолчанию.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("PERIPHSHOP_CONNECTION")
                               ?? "server=localhost;port=3306;database=periphshop;user=periphshop;password=periphshop";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)),
                mysql => mysql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
