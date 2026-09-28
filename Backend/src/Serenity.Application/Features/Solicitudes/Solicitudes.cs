using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Solicitudes;

public sealed record SolicitudDto(
    long Id, string Nombre, string Contacto, DateOnly FechaLlegada, int Huespedes, long? ProyectoId,
    string DestinoTexto, string Estado, string? Notas, DateTime CreadoEn, DateTime ActualizadoEn)
{
    public static SolicitudDto De(SolicitudRegistro s) => new(
        s.Id, s.Nombre, s.Contacto, s.FechaLlegada, s.Huespedes, s.ProyectoId,
        s.DestinoTexto, s.Estado, s.Notas, s.CreadoEn, s.ActualizadoEn);
}

public sealed record SolicitudCreadaDto(long Id);

// ─── Crear (público) ──────────────────────────────────────────────────────────

/// <summary>Cuerpo del formulario público. <see cref="SitioWeb"/> es un honeypot: los humanos no lo ven.</summary>
public sealed record SolicitudInput
{
    public string Nombre { get; init; } = "";
    public string Contacto { get; init; } = "";
    public DateOnly FechaLlegada { get; init; }
    public int Huespedes { get; init; }
    public long? ProyectoId { get; init; }
    public string? DestinoTexto { get; init; }
    public string? SitioWeb { get; init; }
}

/// <summary>Resultado nulo = solicitud descartada en silencio (honeypot).</summary>
public sealed record CrearSolicitudCommand(SolicitudInput Datos) : IRequest<Result<SolicitudCreadaDto?>>;

public sealed partial class CrearSolicitudCommandValidator : AbstractValidator<CrearSolicitudCommand>
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex CorreoRegex();

    [GeneratedRegex(@"^\+?[\d\s\-().]{7,25}$")]
    private static partial Regex TelefonoRegex();

    public static bool EsContactoValido(string? contacto)
    {
        if (string.IsNullOrWhiteSpace(contacto)) return false;
        var c = contacto.Trim();
        if (c.Length > 200) return false;
        if (CorreoRegex().IsMatch(c)) return true;
        return TelefonoRegex().IsMatch(c) && c.Count(char.IsDigit) is >= 7 and <= 15;
    }

    public CrearSolicitudCommandValidator(TimeProvider reloj)
    {
        RuleFor(x => x.Datos).NotNull();
        RuleFor(x => x.Datos.Nombre).Texto(120).When(x => x.Datos is not null);
        RuleFor(x => x.Datos.Contacto).Must(EsContactoValido)
            .WithMessage("Déjanos un correo o un número de WhatsApp/teléfono válido.").When(x => x.Datos is not null);
        RuleFor(x => x.Datos.Huespedes).InclusiveBetween(1, 20).When(x => x.Datos is not null);
        RuleFor(x => x.Datos.FechaLlegada)
            .Must(f => f >= HoyColombia(reloj)).WithMessage("La fecha de llegada no puede estar en el pasado.")
            .Must(f => f <= HoyColombia(reloj).AddYears(2)).WithMessage("La fecha de llegada es demasiado lejana.")
            .When(x => x.Datos is not null);
        RuleFor(x => x.Datos.DestinoTexto).MaximumLength(160).When(x => x.Datos is not null);
        RuleFor(x => x.Datos).Must(d => d.ProyectoId is not null || !string.IsNullOrWhiteSpace(d.DestinoTexto))
            .WithName("Destino").WithMessage("Indica el destino de interés.").When(x => x.Datos is not null);
    }

    /// <summary>Colombia no tiene horario de verano: UTC-5 fijo (evita depender de tzdata en el contenedor).</summary>
    internal static DateOnly HoyColombia(TimeProvider reloj)
        => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime.AddHours(-5));
}

public sealed class CrearSolicitudCommandHandler(ISolicitudRepository solicitudes, IProyectoRepository proyectos)
    : IRequestHandler<CrearSolicitudCommand, Result<SolicitudCreadaDto?>>
{
    public async Task<Result<SolicitudCreadaDto?>> Handle(CrearSolicitudCommand request, CancellationToken ct)
    {
        var d = request.Datos;

        if (!string.IsNullOrWhiteSpace(d.SitioWeb))
        {
            return Result.Success<SolicitudCreadaDto?>(null);
        }

        var destino = d.DestinoTexto?.Trim() ?? "";

        if (d.ProyectoId is { } proyectoId)
        {
            var proyecto = await proyectos.ObtenerAsync(proyectoId, ct);
            if (proyecto is not { Publicado: true })
            {
                return Result.Failure<SolicitudCreadaDto?>("El destino elegido no existe.");
            }

            destino = proyecto.Nombre;
        }

        var id = await solicitudes.CrearAsync(new SolicitudNueva
        {
            Nombre = d.Nombre.Trim(),
            Contacto = d.Contacto.Trim(),
            FechaLlegada = d.FechaLlegada,
            Huespedes = d.Huespedes,
            ProyectoId = d.ProyectoId,
            DestinoTexto = destino
        }, ct);

        return Result.Success<SolicitudCreadaDto?>(new SolicitudCreadaDto(id));
    }
}

// ─── Admin ────────────────────────────────────────────────────────────────────

public sealed record ListarSolicitudesQuery(string? Estado, string? Q, int Page, int PageSize)
    : IRequest<Result<PagedResult<SolicitudDto>>>;

public sealed class ListarSolicitudesQueryHandler(ISolicitudRepository solicitudes)
    : IRequestHandler<ListarSolicitudesQuery, Result<PagedResult<SolicitudDto>>>
{
    public async Task<Result<PagedResult<SolicitudDto>>> Handle(ListarSolicitudesQuery request, CancellationToken ct)
    {
        var estado = string.IsNullOrWhiteSpace(request.Estado) ? null : request.Estado.Trim().ToLowerInvariant();
        if (estado is not null && !EstadosSolicitud.Todos.Contains(estado))
        {
            return Result.Failure<PagedResult<SolicitudDto>>("Estado no válido.");
        }

        var p = new PaginationParams(request.Page, request.PageSize).WithClamp();
        var q = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();
        var (items, total) = await solicitudes.ListarAsync(estado, q, p, ct);

        return Result.Success(new PagedResult<SolicitudDto>(items.Select(SolicitudDto.De).ToList(), p.Page, p.PageSize, total));
    }
}

public sealed record ObtenerSolicitudQuery(long Id) : IRequest<Result<SolicitudDto>>;

public sealed class ObtenerSolicitudQueryHandler(ISolicitudRepository solicitudes) : IRequestHandler<ObtenerSolicitudQuery, Result<SolicitudDto>>
{
    public async Task<Result<SolicitudDto>> Handle(ObtenerSolicitudQuery request, CancellationToken ct)
    {
        var s = await solicitudes.ObtenerAsync(request.Id, ct);
        return s is null ? Result.Failure<SolicitudDto>("La solicitud no existe.", ErrorTipo.NoEncontrado) : Result.Success(SolicitudDto.De(s));
    }
}

public sealed record ActualizarSolicitudInput(string Estado, string? Notas);

public sealed record ActualizarSolicitudCommand(long Id, string Estado, string? Notas) : IRequest<Result<SolicitudDto>>;

public sealed class ActualizarSolicitudCommandValidator : AbstractValidator<ActualizarSolicitudCommand>
{
    public ActualizarSolicitudCommandValidator()
    {
        RuleFor(x => x.Estado).Must(e => EstadosSolicitud.Todos.Contains(e))
            .WithMessage("Estado no válido (nueva, contactada, cerrada o descartada).");
        RuleFor(x => x.Notas).MaximumLength(2000);
    }
}

public sealed class ActualizarSolicitudCommandHandler(ISolicitudRepository solicitudes)
    : IRequestHandler<ActualizarSolicitudCommand, Result<SolicitudDto>>
{
    public async Task<Result<SolicitudDto>> Handle(ActualizarSolicitudCommand request, CancellationToken ct)
    {
        var notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        if (!await solicitudes.ActualizarAsync(request.Id, request.Estado, notas, ct))
        {
            return Result.Failure<SolicitudDto>("La solicitud no existe.", ErrorTipo.NoEncontrado);
        }

        return Result.Success(SolicitudDto.De((await solicitudes.ObtenerAsync(request.Id, ct))!));
    }
}

public sealed record EliminarSolicitudCommand(long Id) : IRequest<Result>;

public sealed class EliminarSolicitudCommandHandler(ISolicitudRepository solicitudes) : IRequestHandler<EliminarSolicitudCommand, Result>
{
    public async Task<Result> Handle(EliminarSolicitudCommand request, CancellationToken ct)
        => await solicitudes.EliminarAsync(request.Id, ct)
            ? Result.Success()
            : Result.Failure("La solicitud no existe.", ErrorTipo.NoEncontrado);
}
