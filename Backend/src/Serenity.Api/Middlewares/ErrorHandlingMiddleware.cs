using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Serenity.Api.Middlewares;

/// <summary>
/// Convierte excepciones en respuestas ProblemDetails (RFC 7807). Va primero en el pipeline.
/// </summary>
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión: no es un error. 499 = "client closed request" (nginx).
            logger.LogDebug("Petición cancelada por el cliente: {Path}", context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (ValidationException ex)
        {
            logger.LogInformation("Validación fallida en {Path}: {Errores}", context.Request.Path,
                string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            await EscribirAsync(context, StatusCodes.Status400BadRequest, "Solicitud inválida",
                "Uno o más campos no son válidos.",
                ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }
        catch (BadHttpRequestException ex)
        {
            await EscribirAsync(context, ex.StatusCode, ex.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Archivo demasiado grande" : "Solicitud inválida", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
            await EscribirAsync(context, StatusCodes.Status500InternalServerError, "Error interno",
                "Ocurrió un error inesperado.");
        }
    }

    private static async Task EscribirAsync(HttpContext context, int status, string titulo, string detalle, object? errores = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails { Status = status, Title = titulo, Detail = detalle, Instance = context.Request.Path };
        if (errores is not null)
        {
            problem.Extensions["errors"] = errores;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
