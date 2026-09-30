using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Anuncios;

/// <summary>Ajustes de la sincronización (sección <c>Airbnb</c> de la configuración).</summary>
public sealed class OpcionesAirbnb
{
    /// <summary>Cada cuánto recorre el worker todos los iCal.</summary>
    public int SyncMinutos { get; init; } = 5;

    /// <summary>Si el último intento es más viejo que esto, consultar la disponibilidad vuelve a descargar el iCal.</summary>
    public int RefrescoBajoDemandaSegundos { get; init; } = 120;
}

public sealed record AnuncioAdminDto(
    long Id, long ProyectoId, string Nombre, string AirbnbRoomId, string UrlAnuncio, string? IcalUrl, bool Publicado,
    DateTime? UltimaSyncIntento, DateTime? UltimaSyncOk, string? UltimaSyncError)
{
    internal static AnuncioAdminDto De(AnuncioRegistro a) => new(
        a.Id, a.ProyectoId, a.Nombre, a.AirbnbRoomId, a.UrlAnuncio, a.IcalUrl, a.Publicado,
        a.UltimaSyncIntento, a.UltimaSyncOk, a.UltimaSyncError);
}

// ─── Sincronización ───────────────────────────────────────────────────────────

public sealed class AnuncioSyncService(IAnuncioRepository anuncios, IIcalClient ical, ILogger<AnuncioSyncService> log)
{
    /// <summary>Descarga y guarda el iCal del anuncio. Nunca lanza: un fallo se registra en el anuncio y se conservan los bloqueos previos.</summary>
    public async Task<Result<int>> SincronizarAsync(AnuncioRegistro anuncio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(anuncio.IcalUrl))
        {
            return Result.Failure<int>("El anuncio no tiene enlace iCal configurado.");
        }

        try
        {
            var contenido = await ical.DescargarAsync(anuncio.IcalUrl, ct);
            var bloqueos = IcalParser.LeerBloqueos(contenido);
            await anuncios.GuardarSincronizacionAsync(anuncio.Id, bloqueos, ct);
            return Result.Success(bloqueos.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // El mensaje no incluye la URL (lleva un secreto) y se recorta para la columna de estado.
            var mensaje = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
            log.LogWarning("Falló la sincronización del anuncio {AnuncioId}: {Motivo}", anuncio.Id, ex.GetType().Name + ": " + mensaje);
            await anuncios.GuardarErrorSincronizacionAsync(anuncio.Id, mensaje, CancellationToken.None);
            return Result.Failure<int>(mensaje);
        }
    }
}

// ─── Datos comunes ────────────────────────────────────────────────────────────

public sealed record AnuncioInput
{
    public string Nombre { get; init; } = "";
    public string UrlAnuncio { get; init; } = "";
    public string? IcalUrl { get; init; }
    public bool Publicado { get; init; } = true;

    internal AnuncioDatos ADatos() => new()
    {
        Nombre = Nombre.Trim(),
        AirbnbRoomId = AirbnbUrls.IdDeAnuncio(UrlAnuncio) ?? "",
        UrlAnuncio = UrlAnuncio.Trim(),
        IcalUrl = string.IsNullOrWhiteSpace(IcalUrl) ? null : IcalUrl.Trim(),
        Publicado = Publicado
    };
}

public sealed class AnuncioInputValidator : AbstractValidator<AnuncioInput>
{
    public AnuncioInputValidator()
    {
        RuleFor(x => x.Nombre).Texto(120);
        RuleFor(x => x.UrlAnuncio).NotEmpty().MaximumLength(500)
            .Must(u => AirbnbUrls.IdDeAnuncio(u) is not null)
            .WithMessage("La URL del anuncio debe ser de Airbnb, con la forma https://www.airbnb.com/rooms/123456.");
        RuleFor(x => x.IcalUrl).MaximumLength(600)
            .Must(u => AirbnbUrls.EsHostAirbnbHttps(u) && u!.Trim().Contains(".ics", StringComparison.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.IcalUrl))
            .WithMessage("El enlace iCal debe ser https, de un dominio de Airbnb, y terminar en .ics.");
        // Cruce de seguridad: un iCal cuyo id no es el del anuncio es casi seguro el calendario de otro anuncio.
        RuleFor(x => x).Must(x => AirbnbUrls.IdDeIcal(x.IcalUrl) is not { } idIcal || idIcal == AirbnbUrls.IdDeAnuncio(x.UrlAnuncio))
            .When(x => !string.IsNullOrWhiteSpace(x.IcalUrl) && AirbnbUrls.IdDeAnuncio(x.UrlAnuncio) is not null)
            .WithName("IcalUrl").WithMessage("El enlace iCal pertenece a otro anuncio: su id no coincide con el de la URL del anuncio.");
    }
}

// ─── Admin: queries y comandos ────────────────────────────────────────────────

public sealed record ListarAnunciosQuery(long ProyectoId) : IRequest<Result<IReadOnlyList<AnuncioAdminDto>>>;

public sealed class ListarAnunciosQueryHandler(IProyectoRepository proyectos, IAnuncioRepository anuncios)
    : IRequestHandler<ListarAnunciosQuery, Result<IReadOnlyList<AnuncioAdminDto>>>
{
    public async Task<Result<IReadOnlyList<AnuncioAdminDto>>> Handle(ListarAnunciosQuery request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure<IReadOnlyList<AnuncioAdminDto>>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var lista = await anuncios.ListarPorProyectoAsync(request.ProyectoId, soloPublicados: false, ct);
        return Result.Success<IReadOnlyList<AnuncioAdminDto>>(lista.Select(AnuncioAdminDto.De).ToList());
    }
}

public sealed record CrearAnuncioCommand(long ProyectoId, AnuncioInput Datos) : IRequest<Result<AnuncioAdminDto>>;

public sealed class CrearAnuncioCommandValidator : AbstractValidator<CrearAnuncioCommand>
{
    public CrearAnuncioCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new AnuncioInputValidator());
}

public sealed class CrearAnuncioCommandHandler(
    IProyectoRepository proyectos, IAnuncioRepository anuncios, AnuncioSyncService sync)
    : IRequestHandler<CrearAnuncioCommand, Result<AnuncioAdminDto>>
{
    public async Task<Result<AnuncioAdminDto>> Handle(CrearAnuncioCommand request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure<AnuncioAdminDto>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var datos = request.Datos.ADatos();
        if (await anuncios.RoomIdExisteAsync(datos.AirbnbRoomId, null, ct))
        {
            return Result.Failure<AnuncioAdminDto>("Ese anuncio de Airbnb ya está registrado.", ErrorTipo.Conflicto);
        }

        var id = await anuncios.CrearAsync(request.ProyectoId, datos, ct);
        var creado = (await anuncios.ObtenerAsync(id, ct))!;

        if (creado.IcalUrl is not null)
        {
            await sync.SincronizarAsync(creado, ct);
            creado = (await anuncios.ObtenerAsync(id, ct))!;
        }

        return Result.Success(AnuncioAdminDto.De(creado));
    }
}

public sealed record ActualizarAnuncioCommand(long Id, AnuncioInput Datos) : IRequest<Result<AnuncioAdminDto>>;

public sealed class ActualizarAnuncioCommandValidator : AbstractValidator<ActualizarAnuncioCommand>
{
    public ActualizarAnuncioCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new AnuncioInputValidator());
}

public sealed class ActualizarAnuncioCommandHandler(IAnuncioRepository anuncios, AnuncioSyncService sync)
    : IRequestHandler<ActualizarAnuncioCommand, Result<AnuncioAdminDto>>
{
    public async Task<Result<AnuncioAdminDto>> Handle(ActualizarAnuncioCommand request, CancellationToken ct)
    {
        var actual = await anuncios.ObtenerAsync(request.Id, ct);
        if (actual is null)
        {
            return Result.Failure<AnuncioAdminDto>("El anuncio no existe.", ErrorTipo.NoEncontrado);
        }

        var datos = request.Datos.ADatos();
        if (await anuncios.RoomIdExisteAsync(datos.AirbnbRoomId, request.Id, ct))
        {
            return Result.Failure<AnuncioAdminDto>("Ese anuncio de Airbnb ya está registrado.", ErrorTipo.Conflicto);
        }

        await anuncios.ActualizarAsync(request.Id, datos, ct);
        var nuevo = (await anuncios.ObtenerAsync(request.Id, ct))!;

        // Solo se descarga si el enlace cambió (el repositorio descartó el estado anterior).
        if (nuevo.IcalUrl is not null && nuevo.IcalUrl != actual.IcalUrl)
        {
            await sync.SincronizarAsync(nuevo, ct);
            nuevo = (await anuncios.ObtenerAsync(request.Id, ct))!;
        }

        return Result.Success(AnuncioAdminDto.De(nuevo));
    }
}

public sealed record EliminarAnuncioCommand(long Id) : IRequest<Result>;

public sealed class EliminarAnuncioCommandHandler(IAnuncioRepository anuncios) : IRequestHandler<EliminarAnuncioCommand, Result>
{
    public async Task<Result> Handle(EliminarAnuncioCommand request, CancellationToken ct)
        => await anuncios.EliminarAsync(request.Id, ct)
            ? Result.Success()
            : Result.Failure("El anuncio no existe.", ErrorTipo.NoEncontrado);
}

/// <summary>Botón «Probar y sincronizar»: descarga ya, sin esperar al worker. Devuelve el anuncio con su estado (y el error, si lo hubo).</summary>
public sealed record SincronizarAnuncioCommand(long Id) : IRequest<Result<AnuncioAdminDto>>;

public sealed class SincronizarAnuncioCommandHandler(IAnuncioRepository anuncios, AnuncioSyncService sync)
    : IRequestHandler<SincronizarAnuncioCommand, Result<AnuncioAdminDto>>
{
    public async Task<Result<AnuncioAdminDto>> Handle(SincronizarAnuncioCommand request, CancellationToken ct)
    {
        var anuncio = await anuncios.ObtenerAsync(request.Id, ct);
        if (anuncio is null)
        {
            return Result.Failure<AnuncioAdminDto>("El anuncio no existe.", ErrorTipo.NoEncontrado);
        }

        if (anuncio.IcalUrl is null)
        {
            return Result.Failure<AnuncioAdminDto>("El anuncio no tiene enlace iCal configurado.");
        }

        await sync.SincronizarAsync(anuncio, ct);
        return Result.Success(AnuncioAdminDto.De((await anuncios.ObtenerAsync(request.Id, ct))!));
    }
}

// ─── Público: disponibilidad ──────────────────────────────────────────────────

/// <summary>Noches ocupadas [Desde, Hasta): <c>Hasta</c> es el día de salida, que sigue libre para llegar.</summary>
public sealed record RangoOcupadoDto(DateOnly Desde, DateOnly Hasta);

public sealed record AnuncioDisponibilidadDto(long Id, string Nombre, string UrlAnuncio, bool Sincronizado, IReadOnlyList<RangoOcupadoDto> Ocupado);

public sealed record DisponibilidadDto(
    DateOnly Desde, DateOnly Hasta, IReadOnlyList<AnuncioDisponibilidadDto> Anuncios, IReadOnlyList<RangoOcupadoDto> OcupadoProyecto);

public sealed record ObtenerDisponibilidadQuery(string Slug, DateOnly? Desde, DateOnly? Hasta) : IRequest<Result<DisponibilidadDto>>;

public sealed class ObtenerDisponibilidadQueryHandler(
    IProyectoRepository proyectos, IAnuncioRepository anuncios, AnuncioSyncService sync, OpcionesAirbnb opciones, TimeProvider reloj)
    : IRequestHandler<ObtenerDisponibilidadQuery, Result<DisponibilidadDto>>
{
    private const int MaxDias = 400;

    public async Task<Result<DisponibilidadDto>> Handle(ObtenerDisponibilidadQuery request, CancellationToken ct)
    {
        // Colombia no tiene horario de verano: UTC-5 fijo.
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime.AddHours(-5));
        var desde = request.Desde ?? hoy;
        var hasta = request.Hasta ?? desde.AddDays(365);

        if (hasta <= desde || hasta.DayNumber - desde.DayNumber > MaxDias)
        {
            return Result.Failure<DisponibilidadDto>($"El rango debe ser positivo y de a lo sumo {MaxDias} días.");
        }

        var proyecto = (await proyectos.ListarAsync(soloPublicados: true, ct)).FirstOrDefault(p => p.Slug == request.Slug);
        if (proyecto is null)
        {
            return Result.Failure<DisponibilidadDto>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var lista = await anuncios.ListarPorProyectoAsync(proyecto.Id, soloPublicados: true, ct);

        // Refresco bajo demanda: el visitante ve lo que Airbnb publica ahora, no lo de hace 5 minutos.
        // El turno se reserva en la base, así que muchas visitas seguidas descargan el .ics una sola vez.
        var refrescos = lista.Where(a => a.IcalUrl is not null).Select(async a =>
        {
            if (await anuncios.TomarTurnoSincronizacionAsync(a.Id, opciones.RefrescoBajoDemandaSegundos, ct))
            {
                await sync.SincronizarAsync(a, ct);
            }
        });
        await Task.WhenAll(refrescos);

        if (lista.Any(a => a.IcalUrl is not null))
        {
            lista = await anuncios.ListarPorProyectoAsync(proyecto.Id, soloPublicados: true, ct);
        }

        var bloqueos = (await anuncios.BloqueosAsync(lista.Select(a => a.Id).ToList(), desde, hasta, ct)).ToLookup(b => b.AnuncioId);

        var porAnuncio = lista.Select(a => new AnuncioDisponibilidadDto(
            a.Id, a.Nombre, a.UrlAnuncio, a.UltimaSyncOk is not null,
            Fusionar(bloqueos[a.Id].Select(b => (b.Desde, b.Hasta)), desde, hasta))).ToList();

        // El proyecto solo está ocupado un día si TODOS sus anuncios lo están y todos están sincronizados;
        // con un anuncio sin calendario no se puede afirmar que no haya disponibilidad.
        var ocupadoProyecto = porAnuncio.Count > 0 && porAnuncio.All(a => a.Sincronizado)
            ? Interseccion(porAnuncio.Select(a => a.Ocupado).ToList())
            : [];

        return Result.Success(new DisponibilidadDto(desde, hasta, porAnuncio, ocupadoProyecto));
    }

    /// <summary>Recorta al rango pedido, ordena y une los tramos que se tocan o se solapan.</summary>
    internal static IReadOnlyList<RangoOcupadoDto> Fusionar(IEnumerable<(DateOnly Desde, DateOnly Hasta)> tramos, DateOnly desde, DateOnly hasta)
    {
        var resultado = new List<RangoOcupadoDto>();
        foreach (var (d, h) in tramos
                     .Select(t => (Desde: t.Desde < desde ? desde : t.Desde, Hasta: t.Hasta > hasta ? hasta : t.Hasta))
                     .Where(t => t.Hasta > t.Desde)
                     .OrderBy(t => t.Desde))
        {
            if (resultado.Count > 0 && d <= resultado[^1].Hasta)
            {
                if (h > resultado[^1].Hasta)
                {
                    resultado[^1] = resultado[^1] with { Hasta = h };
                }
            }
            else
            {
                resultado.Add(new RangoOcupadoDto(d, h));
            }
        }

        return resultado;
    }

    /// <summary>Noches ocupadas en todas las listas a la vez (cada lista ya viene fusionada y ordenada).</summary>
    internal static IReadOnlyList<RangoOcupadoDto> Interseccion(IReadOnlyList<IReadOnlyList<RangoOcupadoDto>> listas)
    {
        if (listas.Count == 0)
        {
            return [];
        }

        IReadOnlyList<RangoOcupadoDto> acumulado = listas[0];
        for (var i = 1; i < listas.Count && acumulado.Count > 0; i++)
        {
            var siguiente = new List<RangoOcupadoDto>();
            int a = 0, b = 0;
            while (a < acumulado.Count && b < listas[i].Count)
            {
                var d = acumulado[a].Desde > listas[i][b].Desde ? acumulado[a].Desde : listas[i][b].Desde;
                var h = acumulado[a].Hasta < listas[i][b].Hasta ? acumulado[a].Hasta : listas[i][b].Hasta;
                if (h > d)
                {
                    siguiente.Add(new RangoOcupadoDto(d, h));
                }

                if (acumulado[a].Hasta < listas[i][b].Hasta) a++; else b++;
            }

            acumulado = siguiente;
        }

        return acumulado;
    }
}
