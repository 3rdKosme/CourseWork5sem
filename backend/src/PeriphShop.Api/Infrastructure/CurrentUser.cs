using System.Security.Claims;
using PeriphShop.Application.Abstractions;
using PeriphShop.Domain.Enums;

namespace PeriphShop.Api.Infrastructure;

/// <summary>Названия политик авторизации по матрице прав (раздел 2.2 ТЗ).</summary>
public static class Policies
{
    public const string Staff = "Staff";
    public const string AdminOnly = "AdminOnly";
    public const string LoginLimiter = "login";
}

/// <summary>Сведения о текущем запросе: пользователь из JWT и идентификатор анонимной корзины.</summary>
public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string CartHeader = "X-Cart-Id";

    private HttpContext? Context => accessor.HttpContext;

    public int? UserId =>
        int.TryParse(Context?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Context?.User.FindFirstValue(ClaimTypes.Email);

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Context?.User.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    public string? Ip => Context?.Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;

    public string? AnonymousCartId
    {
        get
        {
            var value = Context?.Request.Headers[CartHeader].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
