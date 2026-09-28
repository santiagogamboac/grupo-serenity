using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Valores;

public sealed record ValorDto(long Id, string Titulo, string Descripcion, string Icono, string Variante, int Orden, bool Activo)
{
    public static ValorDto De(ValorRegistro v) => new(v.Id, v.Titulo, v.Descripcion, v.Icono, v.Variante, v.Orden, v.Activo);
}

public sealed record ValorInput
{
    public string Titulo { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Icono { get; init; } = "";
    public string Variante { get; init; } = "normal";
    public bool Activo { get; init; } = true;

    internal ValorDatos ADatos() => new()
    {
        Titulo = Titulo.Trim(), Descripcion = Descripcion.Trim(), Icono = Icono.Trim(),
        Variante = Variante.Trim(), Activo = Activo
    };
}

public sealed class ValorInputValidator : AbstractValidator<ValorInput>
{
    public ValorInputValidator()
    {
        RuleFor(x => x.Titulo).Texto(80);
        RuleFor(x => x.Descripcion).Texto(300);
        RuleFor(x => x.Icono).Must(i => Catalogos.Iconos.Contains(i)).WithMessage("Icono no válido. Usa uno de la lista de catálogos.");
        RuleFor(x => x.Variante).Must(v => Catalogos.VariantesValor.Contains(v)).WithMessage("Variante no válida (normal, alta o ancha).");
    }
}

public sealed record ListarValoresAdminQuery : IRequest<IReadOnlyList<ValorDto>>;

public sealed class ListarValoresAdminQueryHandler(IValorRepository valores) : IRequestHandler<ListarValoresAdminQuery, IReadOnlyList<ValorDto>>
{
    public async Task<IReadOnlyList<ValorDto>> Handle(ListarValoresAdminQuery request, CancellationToken ct)
        => (await valores.ListarAsync(false, ct)).Select(ValorDto.De).ToList();
}

public sealed record ObtenerValorQuery(long Id) : IRequest<Result<ValorDto>>;

public sealed class ObtenerValorQueryHandler(IValorRepository valores) : IRequestHandler<ObtenerValorQuery, Result<ValorDto>>
{
    public async Task<Result<ValorDto>> Handle(ObtenerValorQuery request, CancellationToken ct)
    {
        var v = await valores.ObtenerAsync(request.Id, ct);
        return v is null ? Result.Failure<ValorDto>("El valor no existe.", ErrorTipo.NoEncontrado) : Result.Success(ValorDto.De(v));
    }
}

public sealed record CrearValorCommand(ValorInput Datos) : IRequest<Result<ValorDto>>;

public sealed class CrearValorCommandValidator : AbstractValidator<CrearValorCommand>
{
    public CrearValorCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ValorInputValidator());
}

public sealed class CrearValorCommandHandler(IValorRepository valores) : IRequestHandler<CrearValorCommand, Result<ValorDto>>
{
    public async Task<Result<ValorDto>> Handle(CrearValorCommand request, CancellationToken ct)
    {
        var id = await valores.CrearAsync(request.Datos.ADatos(), ct);
        return Result.Success(ValorDto.De((await valores.ObtenerAsync(id, ct))!));
    }
}

public sealed record ActualizarValorCommand(long Id, ValorInput Datos) : IRequest<Result<ValorDto>>;

public sealed class ActualizarValorCommandValidator : AbstractValidator<ActualizarValorCommand>
{
    public ActualizarValorCommandValidator() => RuleFor(x => x.Datos).NotNull().SetValidator(new ValorInputValidator());
}

public sealed class ActualizarValorCommandHandler(IValorRepository valores) : IRequestHandler<ActualizarValorCommand, Result<ValorDto>>
{
    public async Task<Result<ValorDto>> Handle(ActualizarValorCommand request, CancellationToken ct)
    {
        if (!await valores.ActualizarAsync(request.Id, request.Datos.ADatos(), ct))
        {
            return Result.Failure<ValorDto>("El valor no existe.", ErrorTipo.NoEncontrado);
        }

        return Result.Success(ValorDto.De((await valores.ObtenerAsync(request.Id, ct))!));
    }
}

public sealed record EliminarValorCommand(long Id) : IRequest<Result>;

public sealed class EliminarValorCommandHandler(IValorRepository valores) : IRequestHandler<EliminarValorCommand, Result>
{
    public async Task<Result> Handle(EliminarValorCommand request, CancellationToken ct)
        => await valores.EliminarAsync(request.Id, ct) ? Result.Success() : Result.Failure("El valor no existe.", ErrorTipo.NoEncontrado);
}

public sealed record ReordenarValoresCommand(IReadOnlyList<long> Ids) : IRequest<Result>;

public sealed class ReordenarValoresCommandHandler(IValorRepository valores) : IRequestHandler<ReordenarValoresCommand, Result>
{
    public async Task<Result> Handle(ReordenarValoresCommand request, CancellationToken ct)
    {
        var existentes = (await valores.ListarAsync(false, ct)).Select(v => v.Id).ToList();
        if (!Ordenamiento.EsPermutacion(request.Ids, existentes))
        {
            return Result.Failure("La lista de ids debe contener exactamente todos los valores, sin repetir.");
        }

        await valores.ReordenarAsync(request.Ids, ct);
        return Result.Success();
    }
}
