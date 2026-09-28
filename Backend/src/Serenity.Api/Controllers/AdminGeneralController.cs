using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serenity.Application.Common;
using Serenity.Application.Features.Contenido;
using Serenity.Application.Features.Media;
using Serenity.Application.Features.Publico;

namespace Serenity.Api.Controllers;

/// <summary>Dashboard, catálogos y contenido de la landing.</summary>
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminGeneralController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
        => Ok(await Mediator.Send(new ObtenerDashboardQuery(), ct));

    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken ct)
        => Ok(await Mediator.Send(new ObtenerCatalogosQuery(), ct));

    [HttpGet("contenido")]
    public async Task<IActionResult> ObtenerContenido(CancellationToken ct)
        => Ok(await Mediator.Send(new ObtenerContenidoAdminQuery(), ct));

    /// <summary>Reemplaza una sección completa. El cuerpo tiene la forma de esa sección.</summary>
    [HttpPut("contenido/{clave}")]
    public async Task<IActionResult> GuardarSeccion(string clave, [FromBody] JsonElement cuerpo, CancellationToken ct)
    {
        if (!SeccionesRegistro.Tipos.ContainsKey(clave))
        {
            return Problema(Result.Failure($"La sección '{clave}' no existe.", ErrorTipo.NoEncontrado));
        }

        var seccion = SeccionesRegistro.Deserializar(clave, cuerpo, out var error);
        if (seccion is null)
        {
            return Problema(Result.Failure(error ?? "Cuerpo inválido."));
        }

        return Responder(await Mediator.Send(new GuardarSeccionCommand(clave, seccion), ct));
    }
}

/// <summary>Biblioteca de imágenes.</summary>
[Route("api/admin/media")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminMediaController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 24, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListarMediaQuery(page, pageSize), ct));

    [HttpPost]
    [RequestSizeLimit(ReglasMedia.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Subir(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null)
        {
            return Problema(Result.Failure("Envía la imagen en el campo 'archivo' (multipart/form-data)."));
        }

        await using var stream = archivo.OpenReadStream();
        return Creado(await Mediator.Send(new SubirMediaCommand(stream, archivo.FileName, archivo.Length), ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
        => Responder(await Mediator.Send(new EliminarMediaCommand(id), ct));
}
