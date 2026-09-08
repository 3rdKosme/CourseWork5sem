using System.Text.Json;
using Microsoft.Extensions.Logging;
using PeriphShop.Application.Abstractions;
using PeriphShop.Domain.Entities;
using PeriphShop.Infrastructure.Persistence;

namespace PeriphShop.Infrastructure.Auditing;

/// <summary>Журнал аудита критичных действий (FR-70). Сбой записи не прерывает бизнес-операцию.</summary>
public class AuditService(AppDbContext db, ICurrentUser currentUser, ILogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task WriteAsync(string action, string entity, object? entityId = null, object? payload = null,
        CancellationToken ct = default)
    {
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUser.UserId,
                Action = action,
                Entity = entity,
                EntityId = entityId?.ToString(),
                Payload = payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions),
                Ip = currentUser.Ip
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось записать событие аудита {Action} для {Entity}", action, entity);
        }
    }
}
