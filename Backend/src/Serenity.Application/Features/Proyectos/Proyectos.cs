using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Proyectos;

public sealed record ProyectoImagenDto(long Id, long MediaId, string Url, string TextoAlt, int Orden);

public sealed record ProyectoAdminDto(
    long Id, string Slug, string Nombre, string Etiqueta, string Icono, string UbicacionEtiqueta,
    string Titulo, string Descripcion, string MetaLugar, string MetaEstancia, string? UrlReserva,
    string CtaTexto, int Orden, bool Publicado, IReadOnlyList<ProyectoImagenDto> Imagenes);

/// <summary>Ensambla DTOs de proyecto (con su galería) a partir de las filas.</summary>
public sealed class ProyectoEnsamblador(IProyectoRepository proyectos, IAlmacenamientoObjetos almacenamiento)
{
    public async Task<IReadOnlyList<ProyectoAdminDto>> EnsamblarAsync(
        IReadOnlyList<ProyectoRegistro> registros, CancellationToken ct)
    {
        if (registros.Count == 0)
        {
            return [];
        }

        var imagenes = (await proyectos.ImagenesAsync(registros.Select(r => r.Id).ToList(), ct))
            .ToLookup(i => i.ProyectoId);

        return registros.Select(r => new ProyectoAdminDto(
            r.Id, r.Slug, r.Nombre, r.Etiqueta, r.Icono, r.UbicacionEtiqueta, r.Titulo, r.Descripcion,
            r.MetaLugar, r.MetaEstancia, r.UrlReserva, r.CtaTexto, r.Orden, r.Publicado,
            imagenes[r.Id].OrderBy(i => i.Orden)
                .Select(i => new ProyectoImagenDto(i.Id, i.MediaId, almacenamiento.ConstruirUrlPublica(i.StorageKey), i.TextoAlt, i.Orden))
                .ToList())).ToList();
    }

    public async Task<ProyectoAdminDto?> EnsamblarUnoAsync(long id, CancellationToken ct)
    {
        var registro = await proyectos.ObtenerAsync(id, ct);
        return registro is null ? null : (await EnsamblarAsync([registro], ct))[0];
    }
}

// ─── Datos comunes de crear/actualizar ────────────────────────────────────────

public sealed record ProyectoInput
{
    public string Nombre { get; init; } = "";
    public string Etiqueta { get; init; } = "";
    public string Icono { get; init; } = "";
    public string UbicacionEtiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string MetaLugar { get; init; } = "";
    public string MetaEstancia { get; init; } = "";
    public string? UrlReserva { get; init; }
    public string CtaTexto { get; init; } = "Ver alojamientos";
    public bool Publicado { get; init; } = true;

    internal ProyectoDatos ADatos() => new()
    {
        Nombre = Nombre.Trim(),
        Etiqueta = Etiqueta.Trim(),
        Icono = Icono.Trim(),
        UbicacionEtiqueta = UbicacionEtiqueta.Trim(),
        Titulo = Titulo.Trim(),
        Descripcion = Descripcion.Trim(),
        MetaLugar = MetaLugar.Trim(),
        MetaEstancia = MetaEstancia.Trim(),
        UrlReserva = string.IsNullOrWhiteSpace(UrlReserva) ? null : UrlReserva.Trim(),
        CtaTexto = CtaTexto.Trim(),
        Publicado = Publicado
    };
}

public sealed class ProyectoInputValidator : AbstractValidator<ProyectoInput>
{
    public ProyectoInputValidator()
    {
        RuleFor(x => x.Nombre).Texto(120);
        RuleFor(x => x.Etiqueta).Texto(40);
        RuleFor(x => x.Icono).Must(i => Catalogos.Iconos.Contains(i))
            .WithMessage("Icono no válido. Usa uno de la lista de catálogos.");
        RuleFor(x => x.UbicacionEtiqueta).Texto(120);
        RuleFor(x => x.Titulo).Texto(120);
        RuleFor(x => x.Descripcion).Texto(800);
        RuleFor(x => x.MetaLugar).Texto(120, requerido: false);
        RuleFor(x => x.MetaEstancia).Texto(120, requerido: false);
        RuleFor(x => x.UrlReserva).MaximumLength(500).EnlaceSeguro();
        RuleFor(x => x.CtaTexto).Texto(60);
    }
}

// ─── Queries ──────────────────────────────────────────────────────────────────

public sealed record ListarProyectosAdminQuery : IRequest<IReadOnlyList<ProyectoAdminDto>>;

public sealed class ListarProyectosAdminQueryHandler(IProyectoRepository proyectos, ProyectoEnsamblador ensamblador)
    : IRequestHandler<ListarProyectosAdminQuery, IReadOnlyList<ProyectoAdminDto>>
{
    public async Task<IReadOnlyList<ProyectoAdminDto>> Handle(ListarProyectosAdminQuery request, CancellationToken ct)
        => await ensamblador.EnsamblarAsync(await proyectos.ListarAsync(soloPublicados: false, ct), ct);
}

public sealed record ObtenerProyectoAdminQuery(long Id) : IRequest<Result<ProyectoAdminDto>>;

public sealed class ObtenerProyectoAdminQueryHandler(ProyectoEnsamblador ensamblador)
    : IRequestHandler<ObtenerProyectoAdminQuery, Result<ProyectoAdminDto>>
{
    public async Task<Result<ProyectoAdminDto>> Handle(ObtenerProyectoAdminQuery request, CancellationToken ct)
    {
        var dto = await ensamblador.EnsamblarUnoAsync(request.Id, ct);
        return dto is null
            ? Result.Failure<ProyectoAdminDto>("El proyecto no existe.", ErrorTipo.NoEncontrado)
            : Result.Success(dto);
    }
}

// ─── Crear / actualizar / eliminar ────────────────────────────────────────────

public sealed record CrearProyectoCommand(ProyectoInput Datos) : IRequest<Result<ProyectoAdminDto>>;

public sealed class CrearProyectoCommandValidator : AbstractValidator<CrearProyectoCommand>
{
    public CrearProyectoCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ProyectoInputValidator());
}

public sealed class CrearProyectoCommandHandler(IProyectoRepository proyectos, ProyectoEnsamblador ensamblador)
    : IRequestHandler<CrearProyectoCommand, Result<ProyectoAdminDto>>
{
    public async Task<Result<ProyectoAdminDto>> Handle(CrearProyectoCommand request, CancellationToken ct)
    {
        var slug = await SlugUnico.GenerarAsync(proyectos, request.Datos.Nombre, null, ct);
        var id = await proyectos.CrearAsync(request.Datos.ADatos(), slug, ct);
        return Result.Success((await ensamblador.EnsamblarUnoAsync(id, ct))!);
    }
}

public sealed record ActualizarProyectoCommand(long Id, ProyectoInput Datos) : IRequest<Result<ProyectoAdminDto>>;

public sealed class ActualizarProyectoCommandValidator : AbstractValidator<ActualizarProyectoCommand>
{
    public ActualizarProyectoCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ProyectoInputValidator());
}

public sealed class ActualizarProyectoCommandHandler(IProyectoRepository proyectos, ProyectoEnsamblador ensamblador)
    : IRequestHandler<ActualizarProyectoCommand, Result<ProyectoAdminDto>>
{
    public async Task<Result<ProyectoAdminDto>> Handle(ActualizarProyectoCommand request, CancellationToken ct)
    {
        // El slug se conserva al editar: cambiarlo rompería enlaces ya compartidos.
        if (!await proyectos.ActualizarAsync(request.Id, request.Datos.ADatos(), ct))
        {
            return Result.Failure<ProyectoAdminDto>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        return Result.Success((await ensamblador.EnsamblarUnoAsync(request.Id, ct))!);
    }
}

public sealed record EliminarProyectoCommand(long Id) : IRequest<Result>;

public sealed class EliminarProyectoCommandHandler(IProyectoRepository proyectos) : IRequestHandler<EliminarProyectoCommand, Result>
{
    public async Task<Result> Handle(EliminarProyectoCommand request, CancellationToken ct)
        => await proyectos.EliminarAsync(request.Id, ct)
            ? Result.Success()
            : Result.Failure("El proyecto no existe.", ErrorTipo.NoEncontrado);
}

public sealed record ReordenarProyectosCommand(IReadOnlyList<long> Ids) : IRequest<Result>;

public sealed class ReordenarProyectosCommandHandler(IProyectoRepository proyectos) : IRequestHandler<ReordenarProyectosCommand, Result>
{
    public async Task<Result> Handle(ReordenarProyectosCommand request, CancellationToken ct)
    {
        var existentes = (await proyectos.ListarAsync(false, ct)).Select(p => p.Id).ToList();
        if (!Ordenamiento.EsPermutacion(request.Ids, existentes))
        {
            return Result.Failure("La lista de ids debe contener exactamente todos los proyectos, sin repetir.");
        }

        await proyectos.ReordenarAsync(request.Ids, ct);
        return Result.Success();
    }
}

// ─── Galería ──────────────────────────────────────────────────────────────────

public sealed record AgregarImagenProyectoCommand(long ProyectoId, long MediaId, string TextoAlt) : IRequest<Result<ProyectoAdminDto>>;

public sealed class AgregarImagenProyectoCommandValidator : AbstractValidator<AgregarImagenProyectoCommand>
{
    public AgregarImagenProyectoCommandValidator()
    {
        RuleFor(x => x.MediaId).GreaterThan(0);
        RuleFor(x => x.TextoAlt).Texto(200, requerido: false);
    }
}

public sealed class AgregarImagenProyectoCommandHandler(
    IProyectoRepository proyectos, IMediaRepository media, ProyectoEnsamblador ensamblador)
    : IRequestHandler<AgregarImagenProyectoCommand, Result<ProyectoAdminDto>>
{
    public async Task<Result<ProyectoAdminDto>> Handle(AgregarImagenProyectoCommand request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure<ProyectoAdminDto>("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        if (await media.ObtenerAsync(request.MediaId, ct) is null)
        {
            return Result.Failure<ProyectoAdminDto>("La imagen no existe en la biblioteca.");
        }

        await proyectos.AgregarImagenAsync(request.ProyectoId, request.MediaId, request.TextoAlt.Trim(), ct);
        return Result.Success((await ensamblador.EnsamblarUnoAsync(request.ProyectoId, ct))!);
    }
}

public sealed record ActualizarAltImagenProyectoCommand(long ProyectoId, long ImagenId, string TextoAlt) : IRequest<Result>;

public sealed class ActualizarAltImagenProyectoCommandValidator : AbstractValidator<ActualizarAltImagenProyectoCommand>
{
    public ActualizarAltImagenProyectoCommandValidator() => RuleFor(x => x.TextoAlt).Texto(200, requerido: false);
}

public sealed class ActualizarAltImagenProyectoCommandHandler(IProyectoRepository proyectos)
    : IRequestHandler<ActualizarAltImagenProyectoCommand, Result>
{
    public async Task<Result> Handle(ActualizarAltImagenProyectoCommand request, CancellationToken ct)
        => await proyectos.ActualizarAltImagenAsync(request.ProyectoId, request.ImagenId, request.TextoAlt.Trim(), ct)
            ? Result.Success()
            : Result.Failure("La imagen del proyecto no existe.", ErrorTipo.NoEncontrado);
}

public sealed record EliminarImagenProyectoCommand(long ProyectoId, long ImagenId) : IRequest<Result>;

public sealed class EliminarImagenProyectoCommandHandler(IProyectoRepository proyectos)
    : IRequestHandler<EliminarImagenProyectoCommand, Result>
{
    public async Task<Result> Handle(EliminarImagenProyectoCommand request, CancellationToken ct)
        => await proyectos.EliminarImagenAsync(request.ProyectoId, request.ImagenId, ct)
            ? Result.Success()
            : Result.Failure("La imagen del proyecto no existe.", ErrorTipo.NoEncontrado);
}

public sealed record ReordenarImagenesProyectoCommand(long ProyectoId, IReadOnlyList<long> Ids) : IRequest<Result>;

public sealed class ReordenarImagenesProyectoCommandHandler(IProyectoRepository proyectos)
    : IRequestHandler<ReordenarImagenesProyectoCommand, Result>
{
    public async Task<Result> Handle(ReordenarImagenesProyectoCommand request, CancellationToken ct)
    {
        if (await proyectos.ObtenerAsync(request.ProyectoId, ct) is null)
        {
            return Result.Failure("El proyecto no existe.", ErrorTipo.NoEncontrado);
        }

        var existentes = (await proyectos.ImagenesAsync([request.ProyectoId], ct)).Select(i => i.Id).ToList();
        if (!Ordenamiento.EsPermutacion(request.Ids, existentes))
        {
            return Result.Failure("La lista de ids debe contener exactamente todas las imágenes del proyecto, sin repetir.");
        }

        await proyectos.ReordenarImagenesAsync(request.ProyectoId, request.Ids, ct);
        return Result.Success();
    }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

internal static class SlugUnico
{
    public static async Task<string> GenerarAsync(IProyectoRepository proyectos, string nombre, long? excluirId, CancellationToken ct)
    {
        var baseSlug = SlugGenerator.Generar(nombre);
        var slug = baseSlug;

        for (var n = 2; await proyectos.SlugExisteAsync(slug, excluirId, ct); n++)
        {
            slug = $"{baseSlug}-{n}";
        }

        return slug;
    }
}
