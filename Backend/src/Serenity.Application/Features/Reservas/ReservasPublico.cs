using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Features.Anuncios;
using Serenity.Application.Features.Media;
using Serenity.Application.Features.Notificaciones;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Reservas;

public sealed record AlojamientoPublicoDto(
    long Id, long ProyectoId, string Nombre, string Descripcion, MediaDto? Imagen, int Capacidad, int HuespedesIncluidos,
    long PrecioNoche, long RecargoHuespedExtra, long TarifaAseo, int EstanciaMinima);

/// <summary>Lo que la landing necesita para ofrecer la reserva en línea.</summary>
public sealed record ReservaDirectaDto(bool PagoEnLinea, IReadOnlyList<AlojamientoPublicoDto> Alojamientos);

/// <summary>Ocupación de alojamientos con refresco bajo demanda del iCal de Airbnb vinculado.</summary>
public sealed class CalendarioAlojamientos(
    IAlojamientoRepository alojamientos, IAnuncioRepository anuncios, AnuncioSyncService sync, OpcionesAirbnb opcionesAirbnb)
{
    public async Task<IReadOnlyList<OcupacionRegistro>> OcupacionAsync(AlojamientoRegistro a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (a.AnuncioId is { } anuncioId && await anuncios.ObtenerAsync(anuncioId, ct) is { IcalUrl: not null } anuncio
            && await anuncios.TomarTurnoSincronizacionAsync(anuncio.Id, opcionesAirbnb.RefrescoBajoDemandaSegundos, ct))
        {
            await sync.SincronizarAsync(anuncio, ct);
        }

        return (await alojamientos.OcupacionAsync([a.Id], desde, hasta, ct)).Select(o => o.ARegistro()).ToList();
    }
}

// ─── Disponibilidad ───────────────────────────────────────────────────────────

public sealed record DisponibilidadAlojamientoDto(long AlojamientoId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<RangoOcupadoDto> Ocupado);

public sealed record DisponibilidadAlojamientoQuery(long Id, DateOnly? Desde, DateOnly? Hasta) : IRequest<Result<DisponibilidadAlojamientoDto>>;

public sealed class DisponibilidadAlojamientoQueryHandler(
    IAlojamientoRepository alojamientos, IProyectoRepository proyectos, CalendarioAlojamientos calendario, TimeProvider reloj)
    : IRequestHandler<DisponibilidadAlojamientoQuery, Result<DisponibilidadAlojamientoDto>>
{
    public async Task<Result<DisponibilidadAlojamientoDto>> Handle(DisponibilidadAlojamientoQuery request, CancellationToken ct)
    {
        var desde = request.Desde ?? FormatoColombia.Hoy(reloj);
        var hasta = request.Hasta ?? desde.AddDays(365);
        if (hasta <= desde || hasta.DayNumber - desde.DayNumber > 400)
        {
            return Result.Failure<DisponibilidadAlojamientoDto>("El rango debe ser positivo y de a lo sumo 400 días.");
        }

        var a = await ReservaPublica.AlojamientoPublicadoAsync(request.Id, alojamientos, proyectos, ct);
        if (a is null)
        {
            return Result.Failure<DisponibilidadAlojamientoDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var ocupado = await calendario.OcupacionAsync(a, desde, hasta, ct);
        return Result.Success(new DisponibilidadAlojamientoDto(a.Id, desde, hasta,
            ObtenerDisponibilidadQueryHandler.Fusionar(ocupado.Select(o => (o.Desde, o.Hasta)), desde, hasta)));
    }
}

// ─── Cotizar ──────────────────────────────────────────────────────────────────

public sealed record CotizarInput(long AlojamientoId, DateOnly Llegada, DateOnly Salida, int Huespedes);

public sealed record CotizacionPublicaDto(CotizacionDto Cotizacion, bool Disponible);

public sealed record CotizarReservaQuery(CotizarInput Datos) : IRequest<Result<CotizacionPublicaDto>>;

public sealed class CotizarReservaQueryHandler(
    IAlojamientoRepository alojamientos, IProyectoRepository proyectos, CalendarioAlojamientos calendario, TimeProvider reloj)
    : IRequestHandler<CotizarReservaQuery, Result<CotizacionPublicaDto>>
{
    public async Task<Result<CotizacionPublicaDto>> Handle(CotizarReservaQuery request, CancellationToken ct)
    {
        var d = request.Datos;
        var a = await ReservaPublica.AlojamientoPublicadoAsync(d.AlojamientoId, alojamientos, proyectos, ct);
        if (a is null)
        {
            return Result.Failure<CotizacionPublicaDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var cotizacion = Cotizador.Cotizar(a, await alojamientos.TarifasAsync([a.Id], ct), d.Llegada, d.Salida, d.Huespedes, FormatoColombia.Hoy(reloj));
        if (cotizacion.IsFailure)
        {
            return Result.Failure<CotizacionPublicaDto>(cotizacion.Error!);
        }

        var libre = !Cotizador.SeCruza(await calendario.OcupacionAsync(a, d.Llegada, d.Salida, ct), d.Llegada, d.Salida);
        return Result.Success(new CotizacionPublicaDto(cotizacion.Value!, libre));
    }
}

// ─── Crear ────────────────────────────────────────────────────────────────────

/// <summary>Cuerpo del formulario de reserva. <see cref="SitioWeb"/> es un honeypot.</summary>
public sealed record ReservaInput
{
    public long AlojamientoId { get; init; }
    public DateOnly Llegada { get; init; }
    public DateOnly Salida { get; init; }
    public int Huespedes { get; init; }
    public string Nombre { get; init; } = "";
    public string Correo { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string? Nota { get; init; }
    public string? SitioWeb { get; init; }
}

public sealed record ReservaCreadaDto(string Codigo, string Token, long Total, bool PagoEnLinea, string? CheckoutUrl, DateTime? ExpiraEn);

/// <summary>Resultado nulo = descartada en silencio (honeypot).</summary>
public sealed record CrearReservaCommand(ReservaInput Datos) : IRequest<Result<ReservaCreadaDto?>>;

public sealed partial class CrearReservaCommandValidator : AbstractValidator<CrearReservaCommand>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex CorreoRegex();

    [GeneratedRegex(@"^\+?[\d\s\-().]{7,25}$")]
    private static partial Regex TelefonoRegex();

    public CrearReservaCommandValidator()
    {
        RuleFor(x => x.Datos).NotNull();
        When(x => x.Datos is not null, () =>
        {
            RuleFor(x => x.Datos.Nombre).Texto(120);
            RuleFor(x => x.Datos.Correo).NotEmpty().MaximumLength(200)
                .Must(c => CorreoRegex().IsMatch(c.Trim())).WithMessage("Escribe un correo válido: ahí te llega la confirmación.");
            RuleFor(x => x.Datos.Telefono).NotEmpty()
                .Must(t => TelefonoRegex().IsMatch(t.Trim()) && t.Count(char.IsDigit) is >= 7 and <= 15)
                .WithMessage("Escribe un número de celular o WhatsApp válido.");
            RuleFor(x => x.Datos.Nota).MaximumLength(1000);
        });
    }
}

public sealed class CrearReservaCommandHandler(
    IAlojamientoRepository alojamientos, IProyectoRepository proyectos, IReservaRepository reservas,
    CalendarioAlojamientos calendario, OpcionesWompi wompi, OpcionesReservas opciones, Notificador notificador, TimeProvider reloj)
    : IRequestHandler<CrearReservaCommand, Result<ReservaCreadaDto?>>
{
    public async Task<Result<ReservaCreadaDto?>> Handle(CrearReservaCommand request, CancellationToken ct)
    {
        var d = request.Datos;
        if (!string.IsNullOrWhiteSpace(d.SitioWeb))
        {
            return Result.Success<ReservaCreadaDto?>(null);
        }

        var a = await ReservaPublica.AlojamientoPublicadoAsync(d.AlojamientoId, alojamientos, proyectos, ct);
        if (a is null)
        {
            return Result.Failure<ReservaCreadaDto?>("El alojamiento elegido no existe.");
        }

        var cotizacion = Cotizador.Cotizar(a, await alojamientos.TarifasAsync([a.Id], ct), d.Llegada, d.Salida, d.Huespedes, FormatoColombia.Hoy(reloj));
        if (cotizacion.IsFailure)
        {
            return Result.Failure<ReservaCreadaDto?>(cotizacion.Error!);
        }

        // Refresca el iCal de Airbnb (si toca) antes de retener: la verificación final la hace el repositorio con lock.
        await calendario.OcupacionAsync(a, d.Llegada, d.Salida, ct);

        var c = cotizacion.Value!;
        var minutos = wompi.Habilitado ? opciones.MinutosRetencion : opciones.MinutosRetencionSinPago;
        var nueva = new ReservaNueva
        {
            Codigo = await ReservaPublica.NuevoCodigoAsync(reservas, ct),
            TokenConsulta = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            AlojamientoId = a.Id,
            Llegada = d.Llegada,
            Salida = d.Salida,
            Huespedes = d.Huespedes,
            Nombre = d.Nombre.Trim(),
            Correo = d.Correo.Trim(),
            Telefono = d.Telefono.Trim(),
            NotaHuesped = string.IsNullOrWhiteSpace(d.Nota) ? null : d.Nota.Trim(),
            Noches = c.Noches,
            Subtotal = c.Subtotal,
            TarifaAseo = c.TarifaAseo,
            Total = c.Total,
            ExpiraEn = reloj.GetUtcNow().UtcDateTime.AddMinutes(Math.Max(5, minutos))
        };

        var id = await reservas.CrearSiDisponibleAsync(nueva, ct);
        if (id is null)
        {
            return Result.Failure<ReservaCreadaDto?>("Esas fechas ya no están disponibles. Elige otras e inténtalo de nuevo.", ErrorTipo.Conflicto);
        }

        var checkout = wompi.Habilitado ? await ReservaPublica.UrlPagoAsync((await reservas.ObtenerAsync(id.Value, ct))!, reservas, wompi, opciones, ct) : null;
        var creada = (await reservas.ObtenerAsync(id.Value, ct))!;
        await notificador.NuevaReservaAsync(creada, wompi.Habilitado, ct);

        return Result.Success<ReservaCreadaDto?>(new ReservaCreadaDto(
            creada.Codigo, creada.TokenConsulta, creada.Total, wompi.Habilitado, checkout, creada.ExpiraEn));
    }
}

// ─── Consultar / pagar / verificar ────────────────────────────────────────────

public sealed record ReservaPublicaDto(
    string Codigo, string Estado, string? MotivoEstado, string AlojamientoNombre, string ProyectoNombre,
    DateOnly Llegada, DateOnly Salida, int Noches, int Huespedes, string Nombre,
    long Subtotal, long TarifaAseo, long Total, string Moneda, DateTime? ExpiraEn,
    bool PagoEnLinea, string? PagoEstado, bool PuedePagar);

public sealed record ObtenerReservaPublicaQuery(string Codigo, string? Token) : IRequest<Result<ReservaPublicaDto>>;

public sealed class ObtenerReservaPublicaQueryHandler(IReservaRepository reservas, OpcionesWompi wompi, TimeProvider reloj)
    : IRequestHandler<ObtenerReservaPublicaQuery, Result<ReservaPublicaDto>>
{
    public async Task<Result<ReservaPublicaDto>> Handle(ObtenerReservaPublicaQuery request, CancellationToken ct)
    {
        await reservas.ExpirarVencidasAsync(ct);
        var r = await ReservaPublica.AutorizarAsync(request.Codigo, request.Token, reservas, ct);
        return r is null
            ? Result.Failure<ReservaPublicaDto>("La reserva no existe.", ErrorTipo.NoEncontrado)
            : Result.Success(ReservaPublica.Dto(r, wompi, reloj));
    }
}

public sealed record UrlPagoDto(string CheckoutUrl);

/// <summary>Nuevo intento de pago (p. ej. después de un pago rechazado), mientras la retención siga vigente.</summary>
public sealed record NuevoPagoReservaCommand(string Codigo, string? Token) : IRequest<Result<UrlPagoDto>>;

public sealed class NuevoPagoReservaCommandHandler(IReservaRepository reservas, OpcionesWompi wompi, OpcionesReservas opciones, TimeProvider reloj)
    : IRequestHandler<NuevoPagoReservaCommand, Result<UrlPagoDto>>
{
    public async Task<Result<UrlPagoDto>> Handle(NuevoPagoReservaCommand request, CancellationToken ct)
    {
        await reservas.ExpirarVencidasAsync(ct);
        var r = await ReservaPublica.AutorizarAsync(request.Codigo, request.Token, reservas, ct);
        if (r is null)
        {
            return Result.Failure<UrlPagoDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        if (!ReservaPublica.PuedePagar(r, wompi, reloj))
        {
            return Result.Failure<UrlPagoDto>(r.PagoEstado == ProcesadorPagos.Pendiente
                ? "Hay un pago en proceso para esta reserva. Espera su resultado antes de intentar de nuevo."
                : "Esta reserva ya no admite pagos en línea.", ErrorTipo.Conflicto);
        }

        return Result.Success(new UrlPagoDto(await ReservaPublica.UrlPagoAsync(r, reservas, wompi, opciones, ct)));
    }
}

public sealed record VerificarPagoInput(string TransaccionId);

/// <summary>Al volver de Wompi con <c>?id=</c>: consulta la transacción en Wompi (no se confía en el navegador) y la aplica.</summary>
public sealed record VerificarPagoReservaCommand(string Codigo, string? Token, string TransaccionId) : IRequest<Result<ReservaPublicaDto>>;

public sealed class VerificarPagoReservaCommandValidator : AbstractValidator<VerificarPagoReservaCommand>
{
    public VerificarPagoReservaCommandValidator()
        => RuleFor(x => x.TransaccionId).NotEmpty().MaximumLength(80).Matches(@"^[A-Za-z0-9\-_]+$");
}

public sealed class VerificarPagoReservaCommandHandler(
    IReservaRepository reservas, IWompiClient cliente, ProcesadorPagos procesador, OpcionesWompi wompi, TimeProvider reloj)
    : IRequestHandler<VerificarPagoReservaCommand, Result<ReservaPublicaDto>>
{
    public async Task<Result<ReservaPublicaDto>> Handle(VerificarPagoReservaCommand request, CancellationToken ct)
    {
        var r = await ReservaPublica.AutorizarAsync(request.Codigo, request.Token, reservas, ct);
        if (r is null)
        {
            return Result.Failure<ReservaPublicaDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        if (wompi.Habilitado)
        {
            var tx = await cliente.ObtenerTransaccionAsync(request.TransaccionId, ct);
            if (tx is not null && Wompi.CodigoDeReferencia(tx.Referencia) == r.Codigo)
            {
                r = await procesador.AplicarAsync(tx, ct) ?? r;
            }
        }

        await reservas.ExpirarVencidasAsync(ct);
        return Result.Success(ReservaPublica.Dto((await reservas.ObtenerAsync(r.Id, ct))!, wompi, reloj));
    }
}

/// <summary>Evento de Wompi. Falla (→ 400) solo si la firma no es válida; lo demás se acepta para que no reintente.</summary>
public sealed record EventoWompiCommand(JsonElement Cuerpo) : IRequest<Result>;

public sealed class EventoWompiCommandHandler(OpcionesWompi wompi, ProcesadorPagos procesador) : IRequestHandler<EventoWompiCommand, Result>
{
    public async Task<Result> Handle(EventoWompiCommand request, CancellationToken ct)
    {
        if (!wompi.Habilitado)
        {
            return Result.Failure("El pago en línea no está configurado.", ErrorTipo.NoEncontrado);
        }

        var tx = Wompi.LeerEventoValido(request.Cuerpo, wompi.SecretoEventos!);
        if (tx is null)
        {
            return Result.Failure("Firma del evento inválida.", ErrorTipo.NoAutorizado);
        }

        var tipo = request.Cuerpo.TryGetProperty("event", out var e) ? e.GetString() : null;
        if (tipo == "transaction.updated")
        {
            await procesador.AplicarAsync(tx, ct);
        }

        return Result.Success();
    }
}

// ─── Feed .ics de salida ──────────────────────────────────────────────────────

public sealed record IcalAlojamientoQuery(string Token) : IRequest<Result<string>>;

public sealed class IcalAlojamientoQueryHandler(IAlojamientoRepository alojamientos, IReservaRepository reservas, TimeProvider reloj)
    : IRequestHandler<IcalAlojamientoQuery, Result<string>>
{
    public async Task<Result<string>> Handle(IcalAlojamientoQuery request, CancellationToken ct)
    {
        var a = request.Token.Length is >= 32 and <= 64 ? await alojamientos.ObtenerPorTokenIcalAsync(request.Token, ct) : null;
        if (a is null)
        {
            return Result.Failure<string>("Calendario no encontrado.", ErrorTipo.NoEncontrado);
        }

        await reservas.ExpirarVencidasAsync(ct);
        var hoy = FormatoColombia.Hoy(reloj);
        // Sin los bloqueos de Airbnb: este feed se importa en Airbnb y no debe devolverle sus propias fechas.
        var ocupado = (await alojamientos.OcupacionAsync([a.Id], hoy.AddDays(-30), hoy.AddYears(2), ct))
            .Where(o => o.Origen != "airbnb").ToList();

        return Result.Success(IcalWriter.Escribir(a, ocupado, reloj.GetUtcNow().UtcDateTime));
    }
}

public static class IcalWriter
{
    public static string Escribir(AlojamientoRegistro a, IReadOnlyList<OcupacionAlojamiento> ocupado, DateTime ahoraUtc)
    {
        var sb = new StringBuilder();
        void L(string linea) => sb.Append(linea).Append("\r\n");

        L("BEGIN:VCALENDAR");
        L("VERSION:2.0");
        L("PRODID:-//Grupo Serenity//Reserva directa//ES");
        L("CALSCALE:GREGORIAN");
        L("METHOD:PUBLISH");
        L($"X-WR-CALNAME:{Escapar(a.Nombre)}");
        foreach (var o in ocupado.OrderBy(o => o.Desde))
        {
            L("BEGIN:VEVENT");
            L($"UID:{o.Origen}-{o.Referencia}-{a.Id}@gruposerenity");
            L($"DTSTAMP:{ahoraUtc:yyyyMMdd'T'HHmmss'Z'}");
            L($"DTSTART;VALUE=DATE:{o.Desde:yyyyMMdd}");
            L($"DTEND;VALUE=DATE:{o.Hasta:yyyyMMdd}");
            L(o.Origen == "reserva" ? "SUMMARY:Reservado (sitio web)" : "SUMMARY:No disponible");
            L("END:VEVENT");
        }

        L("END:VCALENDAR");
        return sb.ToString();
    }

    private static string Escapar(string s) => s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", " ");
}

// ─── Utilidades compartidas ───────────────────────────────────────────────────

internal static class ReservaPublica
{
    private const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static async Task<AlojamientoRegistro?> AlojamientoPublicadoAsync(
        long id, IAlojamientoRepository alojamientos, IProyectoRepository proyectos, CancellationToken ct)
    {
        var a = await alojamientos.ObtenerAsync(id, ct);
        return a is { Publicado: true } && await proyectos.ObtenerAsync(a.ProyectoId, ct) is { Publicado: true } ? a : null;
    }

    public static async Task<string> NuevoCodigoAsync(IReservaRepository reservas, CancellationToken ct)
    {
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            var codigo = "SER-" + new string(bytes.Select(b => Alfabeto[b % Alfabeto.Length]).ToArray());
            if (!await reservas.CodigoExisteAsync(codigo, ct))
            {
                return codigo;
            }
        }
    }

    /// <summary>Busca por código y exige el token (comparación en tiempo constante). Mismo "no existe" en ambos fallos.</summary>
    public static async Task<ReservaRegistro?> AutorizarAsync(string codigo, string? token, IReservaRepository reservas, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || codigo.Length > 20)
        {
            return null;
        }

        var r = await reservas.ObtenerPorCodigoAsync(codigo.Trim().ToUpperInvariant(), ct);
        return r is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(r.TokenConsulta), Encoding.UTF8.GetBytes(token.Trim()))
            ? r
            : null;
    }

    public static bool PuedePagar(ReservaRegistro r, OpcionesWompi wompi, TimeProvider reloj)
        => wompi.Habilitado
           && r.Estado == EstadosReserva.PendientePago
           && r.ExpiraEn > reloj.GetUtcNow().UtcDateTime
           && r.PagoEstado is not (ProcesadorPagos.Aprobada or ProcesadorPagos.Pendiente);

    public static async Task<string> UrlPagoAsync(
        ReservaRegistro r, IReservaRepository reservas, OpcionesWompi wompi, OpcionesReservas opciones, CancellationToken ct)
    {
        var referencia = await reservas.NuevoIntentoPagoAsync(r.Id, ct);
        return Wompi.ConstruirUrlCheckout(wompi, r, referencia, opciones.EnlaceReserva(r.Codigo, r.TokenConsulta));
    }

    public static ReservaPublicaDto Dto(ReservaRegistro r, OpcionesWompi wompi, TimeProvider reloj) => new(
        r.Codigo, r.Estado, r.Estado is EstadosReserva.Cancelada ? r.MotivoEstado : null,
        r.AlojamientoNombre, r.ProyectoNombre, r.Llegada, r.Salida, r.Noches, r.Huespedes, r.Nombre,
        r.Subtotal, r.TarifaAseo, r.Total, r.Moneda, r.Estado == EstadosReserva.PendientePago ? r.ExpiraEn : null,
        wompi.Habilitado, r.PagoEstado, PuedePagar(r, wompi, reloj));
}
