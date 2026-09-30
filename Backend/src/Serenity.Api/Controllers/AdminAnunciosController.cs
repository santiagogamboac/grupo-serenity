using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Features.Anuncios;

namespace Serenity.Api.Controllers;

/// <summary>Anuncios de Airbnb de un proyecto (con su enlace iCal y el estado de sincronización).</summary>
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminAnunciosController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("proyectos/{proyectoId:long}/anuncios")]
    public async Task<IActionResult> Listar(long proyectoId, CancellationToken ct)
        => Responder(await Mediator.Send(new ListarAnunciosQuery(proyectoId), ct));

    [HttpPost("proyectos/{proyectoId:long}/anuncios")]
    public async Task<IActionResult> Crear(long proyectoId, [FromBody] AnuncioInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearAnuncioCommand(proyectoId, body), ct);
        return Creado(result, result.IsSuccess ? $"/api/admin/anuncios/{result.Value!.Id}" : null);
    }

    [HttpPut("anuncios/{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] AnuncioInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarAnuncioCommand(id, body), ct));

    [HttpDelete("anuncios/{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarAnuncioCommand(id), ct));

    /// <summary>Descarga el iCal ahora. 200 con el anuncio; si falló, <c>ultimaSyncError</c> trae el motivo.</summary>
    [HttpPost("anuncios/{id:long}/sincronizar")]
    public async Task<IActionResult> Sincronizar(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new SincronizarAnuncioCommand(id), ct));
}
