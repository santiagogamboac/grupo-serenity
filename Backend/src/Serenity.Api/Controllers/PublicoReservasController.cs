using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serenity.Application.Features.Reservas;

namespace Serenity.Api.Controllers;

/// <summary>Reserva directa: calendario, cotización, creación, consulta y pago con Wompi.</summary>
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicoReservasController(IMediator mediator) : ApiControllerBase(mediator)
{
    /// <summary>Noches ocupadas del alojamiento (reservas, bloqueos y Airbnb), sin decir por qué.</summary>
    [HttpGet("alojamientos/{id:long}/disponibilidad")]
    [EnableRateLimiting("cotizar")]
    public async Task<IActionResult> Disponibilidad(long id, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        return Responder(await Mediator.Send(new DisponibilidadAlojamientoQuery(id, desde, hasta), ct));
    }

    [HttpPost("reservas/cotizar")]
    [EnableRateLimiting("cotizar")]
    public async Task<IActionResult> Cotizar([FromBody] CotizarInput body, CancellationToken ct)
        => Responder(await Mediator.Send(new CotizarReservaQuery(body), ct));

    /// <summary>Crea la reserva y retiene las fechas. 409 si se ocuparon; honeypot lleno → 202 sin persistir.</summary>
    [HttpPost("reservas")]
    [EnableRateLimiting("reservas")]
    public async Task<IActionResult> Crear([FromBody] ReservaInput body, CancellationToken ct)
    {
        var result = await Mediator.Send(new CrearReservaCommand(body), ct);
        if (result.IsFailure)
        {
            return Problema(result);
        }

        return result.Value is null ? Accepted() : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("reservas/{codigo}")]
    public async Task<IActionResult> Obtener(string codigo, [FromQuery] string? t, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Responder(await Mediator.Send(new ObtenerReservaPublicaQuery(codigo, t), ct));
    }

    /// <summary>Nuevo intento de pago en Wompi (devuelve la URL del checkout).</summary>
    [HttpPost("reservas/{codigo}/pago")]
    [EnableRateLimiting("cotizar")]
    public async Task<IActionResult> Pagar(string codigo, [FromQuery] string? t, CancellationToken ct)
        => Responder(await Mediator.Send(new NuevoPagoReservaCommand(codigo, t), ct));

    /// <summary>Al volver de Wompi: consulta la transacción en Wompi y la aplica.</summary>
    [HttpPost("reservas/{codigo}/verificar")]
    [EnableRateLimiting("cotizar")]
    public async Task<IActionResult> Verificar(string codigo, [FromQuery] string? t, [FromBody] VerificarPagoInput body, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Responder(await Mediator.Send(new VerificarPagoReservaCommand(codigo, t, body.TransaccionId), ct));
    }

    /// <summary>URL de eventos registrada en el dashboard de Wompi. 200 = recibido (Wompi reintenta ante cualquier otro código).</summary>
    [HttpPost("pagos/wompi/eventos")]
    public async Task<IActionResult> EventoWompi([FromBody] JsonElement body, CancellationToken ct)
    {
        var result = await Mediator.Send(new EventoWompiCommand(body), ct);
        return result.IsSuccess ? Ok() : Problema(result);
    }

    /// <summary>Feed .ics de un alojamiento (se pega en Airbnb → Importar calendario). El token es el secreto.</summary>
    [HttpGet("ical/{token}.ics")]
    public async Task<IActionResult> Ical(string token, CancellationToken ct)
    {
        var result = await Mediator.Send(new IcalAlojamientoQuery(token), ct);
        if (result.IsFailure)
        {
            return Problema(result);
        }

        Response.Headers.CacheControl = "no-cache";
        return Content(result.Value!, "text/calendar; charset=utf-8");
    }
}
