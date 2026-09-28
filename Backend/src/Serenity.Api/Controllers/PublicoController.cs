using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serenity.Application.Features.Publico;
using Serenity.Application.Features.Solicitudes;

namespace Serenity.Api.Controllers;

/// <summary>Endpoints anónimos que consume la landing.</summary>
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicoController(IMediator mediator) : ApiControllerBase(mediator)
{
    /// <summary>Todo el contenido de la landing en una sola llamada.</summary>
    [HttpGet("home")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        // Sin caché compartida: lo que el admin guarda debe verse al recargar.
        Response.Headers.CacheControl = "no-cache";
        return Ok(await Mediator.Send(new ObtenerHomeQuery(), ct));
    }

    /// <summary>Solicitud de reserva desde el formulario. Honeypot lleno → 202 sin persistir.</summary>
    [HttpPost("solicitudes")]
    [EnableRateLimiting("solicitudes")]
    public async Task<IActionResult> CrearSolicitud([FromBody] SolicitudInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearSolicitudCommand(body), ct);

        if (result.IsFailure)
        {
            return Problema(result);
        }

        return result.Value is null ? Accepted() : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}
