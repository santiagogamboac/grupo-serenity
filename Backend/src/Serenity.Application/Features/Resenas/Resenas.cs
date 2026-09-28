using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Resenas;

public sealed record ResenaDto(long Id, string Autor, string Contexto, string Texto, int Calificacion, int Orden, bool Publicada)
{
    public static ResenaDto De(ResenaRegistro r) => new(r.Id, r.Autor, r.Contexto, r.Texto, r.Calificacion, r.Orden, r.Publicada);
}

public sealed record ResenaInput
{
    public string Autor { get; init; } = "";
    public string Contexto { get; init; } = "";
    public string Texto { get; init; } = "";
    public int Calificacion { get; init; } = 5;
    public bool Publicada { get; init; } = true;

    internal ResenaDatos ADatos() => new()
    {
        Autor = Autor.Trim(), Contexto = Contexto.Trim(), Texto = Texto.Trim(),
        Calificacion = Calificacion, Publicada = Publicada
    };
}

public sealed class ResenaInputValidator : AbstractValidator<ResenaInput>
{
    public ResenaInputValidator()
    {
        RuleFor(x => x.Autor).Texto(120);
        RuleFor(x => x.Contexto).Texto(120, requerido: false);
        RuleFor(x => x.Texto).Texto(600);
        RuleFor(x => x.Calificacion).InclusiveBetween(1, 5).WithMessage("La calificación debe estar entre 1 y 5.");
    }
}

public sealed record ListarResenasAdminQuery : IRequest<IReadOnlyList<ResenaDto>>;

public sealed class ListarResenasAdminQueryHandler(IResenaRepository resenas)
    : IRequestHandler<ListarResenasAdminQuery, IReadOnlyList<ResenaDto>>
{
    public async Task<IReadOnlyList<ResenaDto>> Handle(ListarResenasAdminQuery request, CancellationToken ct)
        => (await resenas.ListarAsync(false, ct)).Select(ResenaDto.De).ToList();
}

public sealed record ObtenerResenaQuery(long Id) : IRequest<Result<ResenaDto>>;

public sealed class ObtenerResenaQueryHandler(IResenaRepository resenas) : IRequestHandler<ObtenerResenaQuery, Result<ResenaDto>>
{
    public async Task<Result<ResenaDto>> Handle(ObtenerResenaQuery request, CancellationToken ct)
    {
        var r = await resenas.ObtenerAsync(request.Id, ct);
        return r is null ? Result.Failure<ResenaDto>("La reseña no existe.", ErrorTipo.NoEncontrado) : Result.Success(ResenaDto.De(r));
    }
}

public sealed record CrearResenaCommand(ResenaInput Datos) : IRequest<Result<ResenaDto>>;

public sealed class CrearResenaCommandValidator : AbstractValidator<CrearResenaCommand>
{
    public CrearResenaCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ResenaInputValidator());
}

public sealed class CrearResenaCommandHandler(IResenaRepository resenas) : IRequestHandler<CrearResenaCommand, Result<ResenaDto>>
{
    public async Task<Result<ResenaDto>> Handle(CrearResenaCommand request, CancellationToken ct)
    {
        var id = await resenas.CrearAsync(request.Datos.ADatos(), ct);
        return Result.Success(ResenaDto.De((await resenas.ObtenerAsync(id, ct))!));
    }
}

public sealed record ActualizarResenaCommand(long Id, ResenaInput Datos) : IRequest<Result<ResenaDto>>;

public sealed class ActualizarResenaCommandValidator : AbstractValidator<ActualizarResenaCommand>
{
    public ActualizarResenaCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ResenaInputValidator());
}

public sealed class ActualizarResenaCommandHandler(IResenaRepository resenas) : IRequestHandler<ActualizarResenaCommand, Result<ResenaDto>>
{
    public async Task<Result<ResenaDto>> Handle(ActualizarResenaCommand request, CancellationToken ct)
    {
        if (!await resenas.ActualizarAsync(request.Id, request.Datos.ADatos(), ct))
        {
            return Result.Failure<ResenaDto>("La reseña no existe.", ErrorTipo.NoEncontrado);
        }

        return Result.Success(ResenaDto.De((await resenas.ObtenerAsync(request.Id, ct))!));
    }
}

public sealed record EliminarResenaCommand(long Id) : IRequest<Result>;

public sealed class EliminarResenaCommandHandler(IResenaRepository resenas) : IRequestHandler<EliminarResenaCommand, Result>
{
    public async Task<Result> Handle(EliminarResenaCommand request, CancellationToken ct)
        => await resenas.EliminarAsync(request.Id, ct) ? Result.Success() : Result.Failure("La reseña no existe.", ErrorTipo.NoEncontrado);
}

public sealed record ReordenarResenasCommand(IReadOnlyList<long> Ids) : IRequest<Result>;

public sealed class ReordenarResenasCommandHandler(IResenaRepository resenas) : IRequestHandler<ReordenarResenasCommand, Result>
{
    public async Task<Result> Handle(ReordenarResenasCommand request, CancellationToken ct)
    {
        var existentes = (await resenas.ListarAsync(false, ct)).Select(r => r.Id).ToList();
        if (!Ordenamiento.EsPermutacion(request.Ids, existentes))
        {
            return Result.Failure("La lista de ids debe contener exactamente todas las reseñas, sin repetir.");
        }

        await resenas.ReordenarAsync(request.Ids, ct);
        return Result.Success();
    }
}
