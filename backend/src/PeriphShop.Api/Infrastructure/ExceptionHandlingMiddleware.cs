using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PeriphShop.Domain.Exceptions;

namespace PeriphShop.Api.Infrastructure;

/// <summary>
/// Преобразует исключения прикладного уровня в ответы RFC 7807 (раздел 7 ТЗ).
/// Детали внутренних ошибок наружу не отдаются — они остаются в логах вместе с traceId.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = traceId }
        };

        switch (exception)
        {
            case ValidationAppException validation:
                problem.Status = validation.StatusCode;
                problem.Title = validation.Message;
                problem.Extensions["errors"] = validation.Errors;
                logger.LogWarning("Ошибка валидации: {Message}", validation.Message);
                break;

            case AppException app:
                problem.Status = app.StatusCode;
                problem.Title = app.Message;
                logger.LogWarning("Бизнес-отказ {Status}: {Message}", app.StatusCode, app.Message);
                break;

            case OperationCanceledException:
                // Клиент разорвал соединение — тело ответа уже никому не нужно.
                logger.LogDebug("Запрос {Path} отменён клиентом", context.Request.Path);
                return;

            default:
                problem.Status = StatusCodes.Status500InternalServerError;
                problem.Title = "Внутренняя ошибка сервера";
                problem.Detail = $"Обратитесь к администратору, указав traceId {traceId}";
                logger.LogError(exception, "Необработанное исключение на {Path}", context.Request.Path);
                break;
        }

        if (context.Response.HasStarted)
        {
            logger.LogWarning("Ответ уже начат, problem+json не отправлен для {Path}", context.Request.Path);
            return;
        }

        problem.Type = $"https://httpstatuses.io/{problem.Status}";
        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }
}

/// <summary>Заголовки безопасности для ответов API (раздел 9 ТЗ).</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";

        await next(context);
    }
}
