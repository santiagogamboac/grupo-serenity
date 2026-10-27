using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;

namespace Serenity.Application.Features.Reservas;

// ─── Galería del alojamiento (la portada sigue siendo imagenMediaId) ──────────

public sealed record AgregarImagenAlojamientoCommand(long AlojamientoId, long MediaId, string TextoAlt) : IRequest<Result<AlojamientoAdminDto>>;

public sealed class AgregarImagenAlojamientoCommandValidator : AbstractValidator<AgregarImagenAlojamientoCommand>
{
    public AgregarImagenAlojamientoCommandValidator()
    {
        RuleFor(x => x.MediaId).GreaterThan(0);
        RuleFor(x => x.TextoAlt).Texto(200, requerido: false);
    }
}

public sealed class AgregarImagenAlojamientoCommandHandler(
    IAlojamientoRepository alojamientos, IMediaRepository media, AlojamientoEnsamblador ensamblador)
    : IRequestHandler<AgregarImagenAlojamientoCommand, Result<AlojamientoAdminDto>>
{
    public async Task<Result<AlojamientoAdminDto>> Handle(AgregarImagenAlojamientoCommand request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.AlojamientoId, ct) is null)
        {
            return Result.Failure<AlojamientoAdminDto>("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        if (await media.ObtenerAsync(request.MediaId, ct) is null)
        {
            return Result.Failure<AlojamientoAdminDto>("La imagen no existe en la biblioteca.");
        }

        await alojamientos.AgregarImagenAsync(request.AlojamientoId, request.MediaId, request.TextoAlt.Trim(), ct);
        return Result.Success((await ensamblador.EnsamblarUnoAsync(request.AlojamientoId, ct))!);
    }
}

public sealed record ActualizarAltImagenAlojamientoCommand(long AlojamientoId, long ImagenId, string TextoAlt) : IRequest<Result>;

public sealed class ActualizarAltImagenAlojamientoCommandValidator : AbstractValidator<ActualizarAltImagenAlojamientoCommand>
{
    public ActualizarAltImagenAlojamientoCommandValidator() => RuleFor(x => x.TextoAlt).Texto(200, requerido: false);
}

public sealed class ActualizarAltImagenAlojamientoCommandHandler(IAlojamientoRepository alojamientos)
    : IRequestHandler<ActualizarAltImagenAlojamientoCommand, Result>
{
    public async Task<Result> Handle(ActualizarAltImagenAlojamientoCommand request, CancellationToken ct)
        => await alojamientos.ActualizarAltImagenAsync(request.AlojamientoId, request.ImagenId, request.TextoAlt.Trim(), ct)
            ? Result.Success()
            : Result.Failure("La imagen del alojamiento no existe.", ErrorTipo.NoEncontrado);
}

public sealed record EliminarImagenAlojamientoCommand(long AlojamientoId, long ImagenId) : IRequest<Result>;

public sealed class EliminarImagenAlojamientoCommandHandler(IAlojamientoRepository alojamientos)
    : IRequestHandler<EliminarImagenAlojamientoCommand, Result>
{
    public async Task<Result> Handle(EliminarImagenAlojamientoCommand request, CancellationToken ct)
        => await alojamientos.EliminarImagenAsync(request.AlojamientoId, request.ImagenId, ct)
            ? Result.Success()
            : Result.Failure("La imagen del alojamiento no existe.", ErrorTipo.NoEncontrado);
}

public sealed record ReordenarImagenesAlojamientoCommand(long AlojamientoId, IReadOnlyList<long> Ids) : IRequest<Result>;

public sealed class ReordenarImagenesAlojamientoCommandHandler(IAlojamientoRepository alojamientos)
    : IRequestHandler<ReordenarImagenesAlojamientoCommand, Result>
{
    public async Task<Result> Handle(ReordenarImagenesAlojamientoCommand request, CancellationToken ct)
    {
        if (await alojamientos.ObtenerAsync(request.AlojamientoId, ct) is null)
        {
            return Result.Failure("El alojamiento no existe.", ErrorTipo.NoEncontrado);
        }

        var existentes = (await alojamientos.ImagenesAsync([request.AlojamientoId], ct)).Select(i => i.Id).ToList();
        if (!Ordenamiento.EsPermutacion(request.Ids, existentes))
        {
            return Result.Failure("La lista de ids debe contener exactamente todas las imágenes del alojamiento, sin repetir.");
        }

        await alojamientos.ReordenarImagenesAsync(request.AlojamientoId, request.Ids, ct);
        return Result.Success();
    }
}
