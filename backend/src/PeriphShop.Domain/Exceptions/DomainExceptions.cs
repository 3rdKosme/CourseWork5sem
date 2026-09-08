namespace PeriphShop.Domain.Exceptions;

/// <summary>Базовое исключение доменного/прикладного уровня; отображается в problem+json.</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

/// <summary>404 — сущность не найдена.</summary>
public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;

    public static NotFoundException For(string entity, object id) => new($"{entity} с идентификатором {id} не найден(а)");
}

/// <summary>409 — конфликт состояния (остаток, дубликат, недопустимый переход статуса).</summary>
public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;
}

/// <summary>422 — нарушено бизнес-правило (например, промокод неприменим).</summary>
public sealed class BusinessRuleException(string message) : AppException(message)
{
    public override int StatusCode => 422;
}

/// <summary>403 — действие запрещено правами или бизнес-правилом доступа.</summary>
public sealed class ForbiddenException(string message) : AppException(message)
{
    public override int StatusCode => 403;
}

/// <summary>400 — ошибка валидации входных данных.</summary>
public sealed class ValidationAppException(string message, IDictionary<string, string[]>? errors = null)
    : AppException(message)
{
    public override int StatusCode => 400;
    public IDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

/// <summary>401 — требуется аутентификация или неверные учётные данные.</summary>
public sealed class UnauthorizedAppException(string message) : AppException(message)
{
    public override int StatusCode => 401;
}
