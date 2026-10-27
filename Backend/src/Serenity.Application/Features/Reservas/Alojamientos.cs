using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Features.Media;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Reservas;

public sealed record TarifaDto(long Id, string Nombre, DateOnly Desde, DateOnly Hasta, long PrecioNoche);

public sealed record BloqueoManualDto(long Id, DateOnly Desde, DateOnly Hasta, string Motivo, DateTime CreadoEn);

public sealed record AlojamientoImagenDto(long Id, long MediaId, string Url, string TextoAlt, int Orden);

public sealed record AlojamientoAdminDto(
    long Id, long ProyectoId, string Nombre, string Descripcion, long? ImagenMediaId, MediaDto? Imagen,
    int Capacidad, int HuespedesIncluidos, long PrecioNoche, long RecargoHuespedExtra, long TarifaAseo,
    int EstanciaMinima, long? AnuncioId, bool Publicado, int Orden, string UrlIcal, IReadOnlyList<TarifaDto> Tarifas,
    IReadOnlyList<AlojamientoImagenDto> Imagenes);

/// <summary>Arma los DTO de alojamientos con su imagen y sus temporadas (una consulta por tipo de dato).</summary>
public sealed class AlojamientoEnsamblador(
    IAlojamientoRepository alojamientos, IMediaRepository media, IAlmacenamientoObjetos almacenamiento, OpcionesReservas opciones)
{
    public async Task<IReadOnlyList<AlojamientoAdminDto>> EnsamblarAsync(IReadOnlyList<AlojamientoRegistro> lista, CancellationToken ct)
    {
        if (lista.Count == 0)
        {
            return [];
        }

        var imagenes = await ImagenesAsync(lista, ct);
        var tarifas = (await alojamientos.TarifasAsync(lista.Select(a => a.Id).ToList(), ct)).ToLookup(t => t.AlojamientoId);
        var galeria = await GaleriaAsync(lista, ct);

        return lista.Select(a => new AlojamientoAdminDto(
            a.Id, a.ProyectoId, a.Nombre, a.Descripcion, a.ImagenMediaId,
            a.ImagenMediaId is { } m ? imagenes.GetValueOrDefault(m) : null,
            a.Capacidad, a.HuespedesIncluidos, a.PrecioNoche, a.RecargoHuespedExtra, a.TarifaAseo, a.EstanciaMinima,
            a.AnuncioId, a.Publicado, a.Orden, UrlIcal(a.TokenIcal),
            tarifas[a.Id].Select(t => new TarifaDto(t.Id, t.Nombre, t.Desde, t.Hasta, t.PrecioNoche)).ToList(),
            galeria[a.Id].ToList())).ToList();
    }

    public async Task<AlojamientoAdminDto?> EnsamblarUnoAsync(long id, CancellationToken ct)
        => await alojamientos.ObtenerAsync(id, ct) is { } a ? (await EnsamblarAsync([a], ct))[0] : null;

    /// <summary>Galería de cada alojamiento (sin la portada), con su URL pública.</summary>
    public async Task<ILookup<long, AlojamientoImagenDto>> GaleriaAsync(IReadOnlyList<AlojamientoRegistro> lista, CancellationToken ct)
        => (await alojamientos.ImagenesAsync(lista.Select(a => a.Id).ToList(), ct)).ToLookup(
            i => i.AlojamientoId,
            i => new AlojamientoImagenDto(i.Id, i.MediaId, almacenamiento.ConstruirUrlPublica(i.StorageKey), i.TextoAlt, i.Orden));

    public async Task<IReadOnlyDictionary<long, MediaDto>> ImagenesAsync(IReadOnlyList<AlojamientoRegistro> lista, CancellationToken ct)
    {
        var ids = lista.Where(a => a.ImagenMediaId is not null).Select(a => a.ImagenMediaId!.Value).Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<long, MediaDto>()
            : (await media.ObtenerPorIdsAsync(ids, ct))
                .ToDictionary(m => m.Id, m => new MediaDto(m.Id, almacenamiento.ConstruirUrlPublica(m.StorageKey), m.NombreOriginal));
    }

    /// <summary>URL absoluta del feed .ics de salida: Airbnb la descarga desde internet.</summary>
    public string UrlIcal(string token) => $"{opciones.UrlSitio.TrimEnd('/')}/api/public/ical/{token}.ics";
}

// ─── Crear / actualizar ───────────────────────────────────────────────────────

public sealed record AlojamientoInput
{
    public string Nombre { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public long? ImagenMediaId { get; init; }
    public int Capacidad { get; init; }
    public int HuespedesIncluidos { get; init; }
    public long PrecioNoche { get; init; }
    public long RecargoHuespedExtra { get; init; }
    public long TarifaAseo { get; init; }
    public int EstanciaMinima { get; init; } = 1;
    public long? AnuncioId { get; init; }
    public bool Publicado { get; init; } = true;

    internal AlojamientoDatos ADatos() => new()
    {
        Nombre = Nombre.Trim(),
        Descripcion = Descripcion.Trim(),
        ImagenMediaId = ImagenMediaId,
        Capacidad = Capacidad,
        HuespedesIncluidos = HuespedesIncluidos,
        PrecioNoche = PrecioNoche,
        RecargoHuespedExtra = RecargoHuespedExtra,
        TarifaAseo = TarifaAseo,
        EstanciaMinima = EstanciaMinima,
        AnuncioId = AnuncioId,
        Publicado = Publicado
    };
}

public sealed class AlojamientoInputValidator : AbstractValidator<AlojamientoInput>
{
    /// <summary>Tope de cordura para precios en pesos (evita errores de dedo con ceros de más).</summary>
    public const long MaxPesos = 100_000_000;

    public AlojamientoInputValidator()
    {
        RuleFor(x => x.Nombre).Texto(120);
        RuleFor(x => x.Descripcion).MaximumLength(2000);
        RuleFor(x => x.Capacidad).InclusiveBetween(1, 50);
        RuleFor(x => x.HuespedesIncluidos).InclusiveBetween(1, 50)
            .LessThanOrEqualTo(x => x.Capacidad).WithMessage("Los huéspedes incluidos no pueden superar la capacidad.");
        RuleFor(x => x.PrecioNoche).InclusiveBetween(1, MaxPesos).WithMessage("El precio por noche debe ser mayor que cero.");
        RuleFor(x => x.RecargoHuespedExtra).InclusiveBetween(0, MaxPesos);
        RuleFor(x => x.TarifaAseo).InclusiveBetween(0, MaxPesos);
        RuleFor(x => x.EstanciaMinima).InclusiveBetween(1, Cotizador.MaxNoches);
    }
}

/// <summary>Reglas que necesitan la base: imagen y anuncio existentes, anuncio del mismo proyecto y sin otro alojamiento.</summary>
internal static class ReglasAlojamiento
{
    public static async Task<Result> ValidarReferenciasAsync(
        AlojamientoDatos d, long proyectoId, long? alojamientoId,
        IMediaRepository media, IAnuncioRepository anuncios, IAlojamientoRepository alojamientos, CancellationToken ct)
    {
        if (d.ImagenMediaId is { } m && await media.ObtenerAsync(m, ct) is null)
        {
            return Result.Failure("La imagen elegida no existe.");
        }

        if (d.AnuncioId is { } a)
        {
            var anuncio = await anuncios.ObtenerAsync(a, ct);
            if (anuncio is null || anuncio.ProyectoId != proyectoId)
            {
                return Result.Failure("El anuncio de Airbnb elegido no pertenece a este proyecto.");
            }

            if (await alojamientos.AnuncioVinculadoAsync(a, alojamientoId, ct))
            {
                return Result.Failure("Ese anuncio de Airbnb ya está vinculado a otro alojamiento.", ErrorTipo.Conflicto);
            }
        }

        return Result.Success();
    }

    public static string NuevoTokenIcal() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}

public sealed record ListarAlojamientosQuery(long ProyectoId) : IRequest<Result<IReadOnlyList<AlojamientoAdminDto>>>;

public sealed class ListarAlojamientosQueryHandler(
    IProyectoRepository proyectos, IAlojamientoRepository alojamientos, AlojamientoEnsamblador ensamblador)
    : IRequestHandler<ListarAlojamientosQuery, Result<IReadOnlyList<AlojamientoAdminDto>>>
{
    public async Task<Result<IReadOnlyList<AlojamientoAdminDto>>> Handle(ListarAlojamientosQuery request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure<IReadOnlyList<AlojamientoAdminDto>>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var lista = await alojamientos.ListarPorProyectoAsync(request.ProyectoId, soloPublicados: false, ct);
        return Result.Success(await ensamblador.EnsamblarAsync(lista, ct));
    }
}

public sealed record CrearAlojamientoCommand(long ProyectoId, AlojamientoInput Datos) : IRequest<Result<AlojamientoAdminDto>>;

public sealed class CrearAlojamientoCommandValidator : AbstractValidator<CrearAlojamientoCommand>
{
    public CrearAlojamientoCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new AlojamientoInputValidator());
}

public sealed class CrearAlojamientoCommandHandler(
    IProyectoRepository proyectos, IAlojamientoRepository alojamientos, IMediaRepository media,
    IAnuncioRepository anuncios, AlojamientoEnsamblador ensamblador)
    : IRequestHandler<CrearAlojamientoCommand, Result<AlojamientoAdminDto>>
{
    public async Task<Result<AlojamientoAdminDto>> Handle(CrearAlojamientoCommand request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure<AlojamientoAdminDto>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var datos = request.Datos.ADatos();
        var reglas = await ReglasAlojamiento.ValidarReferenciasAsync(datos, request.ProyectoId, null, media, anuncios, alojamientos, ct);
        if (reglas.IsFailure)
        {
            return Result.Failure<AlojamientoAdminDto>(reglas.Error!, reglas.Tipo);
        }

        var id = await alojamientos.CrearAsync(request.ProyectoId, datos, ReglasAlojamiento.NuevoTokenIcal(), ct);
        return Result.Success((await ensamblador.EnsamblarAsync([(await alojamientos.ObtenerAsync(id, ct))!], ct))[0]);
    }
}

public sealed record ActualizarAlojamientoCommand(long Id, AlojamientoInput Datos) : IRequest<Result<AlojamientoAdminDto>>;

public sealed class ActualizarAlojamientoCommandValidator : AbstractValidator<ActualizarAlojamientoCommand>
{
    public ActualizarAlojamientoCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new AlojamientoInputValidator());
}

public sealed class ActualizarAlojamientoCommandHandler(
    IAlojamientoRepository alojamientos, IMediaRepository media, IAnuncioRepository anuncios, AlojamientoEnsamblador ensamblador)
    : IRequestHandler<ActualizarAlojamientoCommand, Result<AlojamientoAdminDto>>
{
    public async Task<Result<AlojamientoAdminDto>> Handle(ActualizarAlojamientoCommand request, CancellationToken ct)
    {
        var actual = await alojamientos.ObtenerAsync(request.Id, ct);
        if (actual is null)
        {
            return Result.Failure<AlojamientoAdminDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var datos = request.Datos.ADatos();
        var reglas = await ReglasAlojamiento.ValidarReferenciasAsync(datos, actual.ProyectoId, actual.Id, media, anuncios, alojamientos, ct);
        if (reglas.IsFailure)
        {
            return Result.Failure<AlojamientoAdminDto>(reglas.Error!, reglas.Tipo);
        }

        await alojamientos.ActualizarAsync(request.Id, datos, ct);
        return Result.Success((await ensamblador.EnsamblarAsync([(await alojamientos.ObtenerAsync(request.Id, ct))!], ct))[0]);
    }
}

public sealed record EliminarAlojamientoCommand(long Id) : IRequest<Result>;

public sealed class EliminarAlojamientoCommandHandler(IAlojamientoRepository alojamientos) : IRequestHandler<EliminarAlojamientoCommand, Result>
{
    public async Task<Result> Handle(EliminarAlojamientoCommand request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.Id, ct) is null)
        {
            return Result.Failure("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        if (await alojamientos.TieneReservasAsync(request.Id, ct))
        {
            return Result.Failure("Este alojamiento tiene reservas registradas: no se puede eliminar. Despublícalo para dejar de ofrecerlo.", ErrorTipo.Conflicto);
        }

        await alojamientos.EliminarAsync(request.Id, ct);
        return Result.Success();
    }
}

// ─── Temporadas ───────────────────────────────────────────────────────────────

public sealed record TarifaInput(string Nombre, DateOnly Desde, DateOnly Hasta, long PrecioNoche);

public sealed record CrearTarifaCommand(long AlojamientoId, TarifaInput Datos) : IRequest<Result<TarifaDto>>;

public sealed class CrearTarifaCommandValidator : AbstractValidator<CrearTarifaCommand>
{
    public CrearTarifaCommandValidator()
    {
        RuleFor(x => x.Datos).NotNull();
        RuleFor(x => x.Datos.Nombre).Texto(80).When(x => x.Datos is not null);
        RuleFor(x => x.Datos.PrecioNoche).InclusiveBetween(1, AlojamientoInputValidator.MaxPesos).When(x => x.Datos is not null);
        RuleFor(x => x.Datos).Must(d => d.Hasta > d.Desde).WithName("Hasta")
            .WithMessage("La temporada debe terminar después de empezar.").When(x => x.Datos is not null);
        RuleFor(x => x.Datos).Must(d => d.Hasta.DayNumber - d.Desde.DayNumber <= 400).WithName("Hasta")
            .WithMessage("Una temporada puede durar a lo sumo 400 días.").When(x => x.Datos is not null);
    }
}

public sealed class CrearTarifaCommandHandler(IAlojamientoRepository alojamientos) : IRequestHandler<CrearTarifaCommand, Result<TarifaDto>>
{
    public async Task<Result<TarifaDto>> Handle(CrearTarifaCommand request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.AlojamientoId, ct) is null)
        {
            return Result.Failure<TarifaDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var d = request.Datos;
        var id = await alojamientos.CrearTarifaAsync(request.AlojamientoId, d.Nombre.Trim(), d.Desde, d.Hasta, d.PrecioNoche, ct);
        return id is { } nuevo
            ? Result.Success(new TarifaDto(nuevo, d.Nombre.Trim(), d.Desde, d.Hasta, d.PrecioNoche))
            : Result.Failure<TarifaDto>("Esas fechas se cruzan con otra temporada de este alojamiento.", ErrorTipo.Conflicto);
    }
}

public sealed record EliminarTarifaCommand(long Id) : IRequest<Result>;

public sealed class EliminarTarifaCommandHandler(IAlojamientoRepository alojamientos) : IRequestHandler<EliminarTarifaCommand, Result>
{
    public async Task<Result> Handle(EliminarTarifaCommand request, CancellationToken ct)
        => await alojamientos.EliminarTarifaAsync(request.Id, ct)
            ? Result.Success()
            : Result.Failure("La temporada no existe.", ErrorTipo.NoEncontrado);
}

// ─── Bloqueos manuales ────────────────────────────────────────────────────────

public sealed record ListarBloqueosQuery(long AlojamientoId) : IRequest<Result<IReadOnlyList<BloqueoManualDto>>>;

public sealed class ListarBloqueosQueryHandler(IAlojamientoRepository alojamientos)
    : IRequestHandler<ListarBloqueosQuery, Result<IReadOnlyList<BloqueoManualDto>>>
{
    public async Task<Result<IReadOnlyList<BloqueoManualDto>>> Handle(ListarBloqueosQuery request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.AlojamientoId, ct) is null)
        {
            return Result.Failure<IReadOnlyList<BloqueoManualDto>>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var lista = await alojamientos.BloqueosManualesAsync(request.AlojamientoId, ct);
        return Result.Success<IReadOnlyList<BloqueoManualDto>>(
            lista.Select(b => new BloqueoManualDto(b.Id, b.Desde, b.Hasta, b.Motivo, b.CreadoEn)).ToList());
    }
}

public sealed record BloqueoInput(DateOnly Desde, DateOnly Hasta, string? Motivo);

public sealed record CrearBloqueoCommand(long AlojamientoId, BloqueoInput Datos) : IRequest<Result<BloqueoManualDto>>;

public sealed class CrearBloqueoCommandValidator : AbstractValidator<CrearBloqueoCommand>
{
    public CrearBloqueoCommandValidator()
    {
        RuleFor(x => x.Datos).NotNull();
        RuleFor(x => x.Datos.Motivo).MaximumLength(200).When(x => x.Datos is not null);
        RuleFor(x => x.Datos).Must(d => d.Hasta > d.Desde).WithName("Hasta")
            .WithMessage("El bloqueo debe terminar después de empezar (la fecha final es el día en que vuelve a estar libre).")
            .When(x => x.Datos is not null);
        RuleFor(x => x.Datos).Must(d => d.Hasta.DayNumber - d.Desde.DayNumber <= 400).WithName("Hasta")
            .WithMessage("Un bloqueo puede durar a lo sumo 400 días.").When(x => x.Datos is not null);
    }
}

public sealed class CrearBloqueoCommandHandler(IAlojamientoRepository alojamientos) : IRequestHandler<CrearBloqueoCommand, Result<BloqueoManualDto>>
{
    public async Task<Result<BloqueoManualDto>> Handle(CrearBloqueoCommand request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.AlojamientoId, ct) is null)
        {
            return Result.Failure<BloqueoManualDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var d = request.Datos;
        var motivo = d.Motivo?.Trim() ?? "";
        var id = await alojamientos.CrearBloqueoAsync(request.AlojamientoId, d.Desde, d.Hasta, motivo, ct);
        return Result.Success(new BloqueoManualDto(id, d.Desde, d.Hasta, motivo, DateTime.UtcNow));
    }
}

public sealed record EliminarBloqueoCommand(long Id) : IRequest<Result>;

public sealed class EliminarBloqueoCommandHandler(IAlojamientoRepository alojamientos) : IRequestHandler<EliminarBloqueoCommand, Result>
{
    public async Task<Result> Handle(EliminarBloqueoCommand request, CancellationToken ct)
        => await alojamientos.EliminarBloqueoAsync(request.Id, ct)
            ? Result.Success()
            : Result.Failure("El bloqueo no existe.", ErrorTipo.NoEncontrado);
}
