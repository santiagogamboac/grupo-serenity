using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Features.Reservas;

namespace Serenity.Api.Controllers;

/// <summary>Alojamientos de un proyecto (precios, temporadas, bloqueos) y gestión de reservas.</summary>
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminReservasController(IMediator mediator) : ApiControllerBase(mediator)
{
    // ─── Alojamientos ─────────────────────────────────────────────────────────

    [HttpGet("proyectos/{proyectoId:long}/alojamientos")]
    public async Task<IActionResult> ListarAlojamientos(long proyectoId, CancellationToken ct)
        => Responder(await Mediator.Send(new ListarAlojamientosQuery(proyectoId), ct));

    [HttpPost("proyectos/{proyectoId:long}/alojamientos")]
    public async Task<IActionResult> CrearAlojamiento(long proyectoId, [FromBody] AlojamientoInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearAlojamientoCommand(proyectoId, body), ct);
        return Creado(result, result.IsSuccess ? $"/api/admin/alojamientos/{result.Value!.Id}" : null);
    }

    [HttpPut("alojamientos/{id:long}")]
    public async Task<IActionResult> ActualizarAlojamiento(long id, [FromBody] AlojamientoInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarAlojamientoCommand(id, body), ct));

    [HttpDelete("alojamientos/{id:long}")]
    public async Task<IActionResult> EliminarAlojamiento(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarAlojamientoCommand(id), ct));

    [HttpPost("alojamientos/{id:long}/tarifas")]
    public async Task<IActionResult> CrearTarifa(long id, [FromBody] TarifaInput body, CancellationToken ct)
        => Creado(await Mediator.Send(new CrearTarifaCommand(id, body), ct));

    [HttpDelete("tarifas/{id:long}")]
    public async Task<IActionResult> EliminarTarifa(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarTarifaCommand(id), ct));

    [HttpGet("alojamientos/{id:long}/bloqueos")]
    public async Task<IActionResult> ListarBloqueos(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new ListarBloqueosQuery(id), ct));

    [HttpPost("alojamientos/{id:long}/bloqueos")]
    public async Task<IActionResult> CrearBloqueo(long id, [FromBody] BloqueoInput body, CancellationToken ct)
        => Creado(await Mediator.Send(new CrearBloqueoCommand(id, body), ct));

    [HttpDelete("bloqueos/{id:long}")]
    public async Task<IActionResult> EliminarBloqueo(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarBloqueoCommand(id), ct));

    // ─── Reservas ─────────────────────────────────────────────────────────────

    [HttpGet("reservas")]
    public async Task<IActionResult> ListarReservas(
        [FromQuery] string? estado, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Responder(await Mediator.Send(new ListarReservasQuery(estado, q, page, pageSize), ct));

    [HttpGet("reservas/{id:long}")]
    public async Task<IActionResult> ObtenerReserva(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new ObtenerReservaQuery(id), ct));

    /// <summary>Confirmar a mano (pago recibido por otro medio, o revisión resuelta). 409 si las fechas ya no están libres.</summary>
    [HttpPost("reservas/{id:long}/confirmar")]
    public async Task<IActionResult> Confirmar(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new ConfirmarReservaCommand(id), ct));

    [HttpPost("reservas/{id:long}/cancelar")]
    public async Task<IActionResult> Cancelar(long id, [FromBody] CancelarReservaInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new CancelarReservaCommand(id, body.Motivo ?? "", body.AvisarHuesped), ct));

    [HttpPut("reservas/{id:long}/notas")]
    public async Task<IActionResult> Notas(long id, [FromBody] NotasReservaInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarNotasReservaCommand(id, body.Notas), ct));
}
