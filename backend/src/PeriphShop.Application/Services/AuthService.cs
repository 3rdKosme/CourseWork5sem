using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Enums;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Регистрация, вход, ротация токенов, профиль (FR-10 ... FR-16).</summary>
public class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    ICurrentUser currentUser,
    IBusinessMetrics metrics,
    ICartService carts,
    ILogger<AuthService> logger)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Пользователь с таким e-mail уже зарегистрирован");

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Role = UserRole.Customer,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Зарегистрирован пользователь {UserId} ({Email})", user.Id, user.Email);

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            metrics.LoginAttempt(false);
            logger.LogWarning("Неудачная попытка входа для {Email}", email);
            throw new UnauthorizedAppException("Неверный e-mail или пароль");
        }

        if (!user.IsActive)
        {
            metrics.LoginAttempt(false);
            throw new ForbiddenException("Учётная запись заблокирована");
        }

        metrics.LoginAttempt(true);
        var response = await IssueAsync(user, ct);

        // FR-21: анонимная корзина переносится пользователю сразу после входа.
        await carts.MergeAnonymousCartAsync(user.Id, currentUser.AnonymousCartId, ct);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (stored is null || !stored.IsActive)
            throw new UnauthorizedAppException("Refresh-токен недействителен");
        if (!stored.User.IsActive)
            throw new ForbiddenException("Учётная запись заблокирована");

        // Ротация: использованный токен отзывается (FR-13).
        stored.RevokedAt = DateTime.UtcNow;
        return await IssueAsync(stored.User, ct);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var user = await LoadCurrentUserAsync(ct);
        return Map(user);
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await LoadCurrentUserAsync(ct);

        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.DefaultAddress = string.IsNullOrWhiteSpace(request.DefaultAddress) ? null : request.DefaultAddress.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Map(user);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await LoadCurrentUserAsync(ct);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ForbiddenException("Текущий пароль указан неверно");

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Смена пароля отзывает все активные refresh-токены пользователя.
        var active = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active) token.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    private async Task<User> LoadCurrentUserAsync(CancellationToken ct)
    {
        var id = currentUser.UserId ?? throw new UnauthorizedAppException("Требуется аутентификация");
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
               ?? throw NotFoundException.For("Пользователь", id);
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var pair = tokens.Issue(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = pair.RefreshToken,
            ExpiresAt = pair.RefreshExpiresAt,
            CreatedByIp = currentUser.Ip
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(pair.AccessToken, pair.RefreshToken, pair.AccessExpiresAt, Map(user));
    }

    internal static UserDto Map(User user) =>
        new(user.Id, user.Email, user.FullName, user.Phone, user.DefaultAddress, user.Role.ToString());
}
