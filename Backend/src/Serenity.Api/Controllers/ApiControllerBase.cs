using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Common;

namespace Serenity.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;

    /// <summary>Id del usuario autenticado (claim "sub").</summary>
    protected long? UsuarioId
        => long.TryParse(User.FindFirstValue("sub"), out var id) ? id : null;

    protected IActionResult Problema(Result r)
    {
        var (status, titulo) = r.Tipo switch
        {
            ErrorTipo.NoAutorizado => (StatusCodes.Status401Unauthorized, "No autorizado"),
            ErrorTipo.Bloqueado => (StatusCodes.Status423Locked, "Cuenta bloqueada"),
            ErrorTipo.NoEncontrado => (StatusCodes.Status404NotFound, "No encontrado"),
            ErrorTipo.Conflicto => (StatusCodes.Status409Conflict, "Conflicto"),
            _ => (StatusCodes.Status400BadRequest, "Solicitud inválida")
        };

        return StatusCode(status, new ProblemDetails { Status = status, Title = titulo, Detail = r.Error, Instance = Request.Path });
    }

    /// <summary>200 con el valor, o el ProblemDetails que corresponda.</summary>
    protected IActionResult Responder<T>(Result<T> r) => r.IsSuccess ? Ok(r.Value) : Problema(r);

    /// <summary>204 sin contenido, o el ProblemDetails que corresponda.</summary>
    protected IActionResult Responder(Result r) => r.IsSuccess ? NoContent() : Problema(r);

    protected IActionResult Creado<T>(Result<T> r, string? ubicacion = null)
        => r.IsSuccess ? Created(ubicacion ?? Request.Path, r.Value) : Problema(r);
}
