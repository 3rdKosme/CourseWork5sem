using Microsoft.EntityFrameworkCore;
using PeriphShop.Application.Abstractions;
using PeriphShop.Application.Common;
using PeriphShop.Application.Contracts;
using PeriphShop.Domain.Entities;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Application.Services;

/// <summary>Управление пользователями и ролями, доступно только администратору (FR-16).</summary>
public class UserAdminService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit)
{
    public async Task<PagedResult<AdminUserDto>> SearchAsync(AdminUserQuery query, CancellationToken ct = default)
    {
        var users = db.Users.AsQueryable();

        if (query.Role is { } role) users = users.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u =>
                EF.Functions.Like(u.Email, $"%{term}%") || EF.Functions.Like(u.FullName, $"%{term}%"));
        }

        return await users
            .OrderBy(u => u.Id)
            .Select(u => new AdminUserDto(u.Id, u.Email, u.FullName, u.Phone, u.Role.ToString(),
                u.IsActive, u.Orders.Count, u.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<AdminUserDto> ChangeRoleAsync(int id, ChangeRoleRequest request, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);

        if (user.Id == currentUser.UserId)
            throw new BusinessRuleException("Нельзя изменить собственную роль");

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("UserRoleChanged", nameof(User), id, new { Role = request.Role.ToString() }, ct);

        return Map(user);
    }

    public async Task<AdminUserDto> ToggleActiveAsync(int id, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, ct);

        if (user.Id == currentUser.UserId)
            throw new BusinessRuleException("Нельзя заблокировать собственную учётную запись");

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (!user.IsActive)
        {
            // Блокировка немедленно прекращает действующие сессии.
            var tokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(ct);
            foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("UserActiveToggled", nameof(User), id, new { user.IsActive }, ct);

        return Map(user);
    }

    private async Task<User> LoadAsync(int id, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw NotFoundException.For("Пользователь", id);

    private static AdminUserDto Map(User u) =>
        new(u.Id, u.Email, u.FullName, u.Phone, u.Role.ToString(), u.IsActive, u.Orders.Count, u.CreatedAt);
}
