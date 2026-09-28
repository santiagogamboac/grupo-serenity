using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Common;
using Serenity.Application.Features.Proyectos;

namespace Serenity.Api.Controllers;

[Route("api/admin/proyectos")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminProyectosController(IMediator mediator) : ApiControllerBase(mediator)
{
    public sealed record AgregarImagenRequest(long MediaId, string? TextoAlt);

    public sealed record TextoAltRequest(string? TextoAlt);

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await Mediator.Send(new ListarProyectosAdminQuery(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtener(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new ObtenerProyectoAdminQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ProyectoInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearProyectoCommand(body), ct);
        return Creado(result, result.IsSuccess ? $"/api/admin/proyectos/{result.Value!.Id}" : null);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] ProyectoInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarProyectoCommand(id, body), ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarProyectoCommand(id), ct));

    [HttpPut("orden")]
    public async Task<IActionResult> Reordenar([FromBody] OrdenInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ReordenarProyectosCommand(body.Ids), ct));

    // ─── Galería ──────────────────────────────────────────────────────────────

    [HttpPost("{id:long}/imagenes")]
    public async Task<IActionResult> AgregarImagen(long id, [FromBody] AgregarImagenRequest body, CancellationToken ct)
        => Creado(await Mediator.Send(new AgregarImagenProyectoCommand(id, body.MediaId, body.TextoAlt ?? ""), ct));

    [HttpPut("{id:long}/imagenes/orden")]
    public async Task<IActionResult> ReordenarImagenes(long id, [FromBody] OrdenInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ReordenarImagenesProyectoCommand(id, body.Ids), ct));

    [HttpPut("{id:long}/imagenes/{imagenId:long}")]
    public async Task<IActionResult> ActualizarAlt(long id, long imagenId, [FromBody] TextoAltRequest body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarAltImagenProyectoCommand(id, imagenId, body.TextoAlt ?? ""), ct));

    [HttpDelete("{id:long}/imagenes/{imagenId:long}")]
    public async Task<IActionResult> EliminarImagen(long id, long imagenId, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarImagenProyectoCommand(id, imagenId), ct));
}
