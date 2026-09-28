using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Common;
using Serenity.Application.Features.Resenas;
using Serenity.Application.Features.Solicitudes;
using Serenity.Application.Features.Valores;

namespace Serenity.Api.Controllers;

[Route("api/admin/resenas")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminResenasController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await Mediator.Send(new ListarResenasAdminQuery(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtener(long id, CancellationToken ct) => Responder(await Mediator.Send(new ObtenerResenaQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ResenaInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearResenaCommand(body), ct);
        return Creado(result, result.IsSuccess ? $"/api/admin/resenas/{result.Value!.Id}" : null);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] ResenaInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarResenaCommand(id, body), ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct) => Responder(await Mediator.Send(new EliminarResenaCommand(id), ct));

    [HttpPut("orden")]
    public async Task<IActionResult> Reordenar([FromBody] OrdenInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ReordenarResenasCommand(body.Ids), ct));
}

[Route("api/admin/valores")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminValoresController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct) => Ok(await Mediator.Send(new ListarValoresAdminQuery(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtener(long id, CancellationToken ct) => Responder(await Mediator.Send(new ObtenerValorQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ValorInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearValorCommand(body), ct);
        return Creado(result, result.IsSuccess ? $"/api/admin/valores/{result.Value!.Id}" : null);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] ValorInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarValorCommand(id, body), ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct) => Responder(await Mediator.Send(new EliminarValorCommand(id), ct));

    [HttpPut("orden")]
    public async Task<IActionResult> Reordenar([FromBody] OrdenInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ReordenarValoresCommand(body.Ids), ct));
}

[Route("api/admin/solicitudes")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminSolicitudesController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Responder(await Mediator.Send(new ListarSolicitudesQuery(estado, q, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtener(long id, CancellationToken ct) => Responder(await Mediator.Send(new ObtenerSolicitudQuery(id), ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] ActualizarSolicitudInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new ActualizarSolicitudCommand(id, body.Estado, body.Notas), ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct) => Responder(await Mediator.Send(new EliminarSolicitudCommand(id), ct));
}
