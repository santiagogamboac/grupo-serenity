using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Media;

public sealed record MediaDto(long Id, string Url, string NombreOriginal);

public sealed record MediaDetalleDto(long Id, string Url, string NombreOriginal, string ContentType, long TamanoBytes, DateTime CreadoEn);

// ─── Subir ────────────────────────────────────────────────────────────────────

public sealed record SubirMediaCommand(Stream Contenido, string NombreOriginal, long Longitud) : IRequest<Result<MediaDetalleDto>>;

public sealed class SubirMediaCommandValidator : AbstractValidator<SubirMediaCommand>
{
    public SubirMediaCommandValidator()
    {
        RuleFor(x => x.NombreOriginal).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Longitud)
            .GreaterThan(0).WithMessage("El archivo está vacío.")
            .LessThanOrEqualTo(ReglasMedia.MaxBytes).WithMessage("La imagen supera el máximo de 10 MB.");
    }
}

public sealed class SubirMediaCommandHandler(
    IMediaRepository media,
    IAlmacenamientoObjetos almacenamiento,
    TimeProvider reloj) : IRequestHandler<SubirMediaCommand, Result<MediaDetalleDto>>
{
    public async Task<Result<MediaDetalleDto>> Handle(SubirMediaCommand request, CancellationToken ct)
    {
        // Se copia a memoria (máx. 10 MB, ya validado) para inspeccionar la firma y poder rebobinar.
        using var buffer = new MemoryStream((int)request.Longitud);
        await request.Contenido.CopyToAsync(buffer, ct);

        if (buffer.Length > ReglasMedia.MaxBytes)
        {
            return Result.Failure<MediaDetalleDto>("La imagen supera el máximo de 10 MB.");
        }

        var tipo = ReglasMedia.DetectarTipo(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 16)));
        if (tipo is null)
        {
            return Result.Failure<MediaDetalleDto>("Formato no soportado. Usa JPEG, PNG, WebP o GIF.");
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var storageKey = $"{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}.{tipo.Value.Extension}";

        // El SDK de S3 cierra el stream al terminar: el tamaño se toma antes.
        var tamano = buffer.Length;
        buffer.Position = 0;
        await almacenamiento.SubirAsync(storageKey, buffer, tipo.Value.ContentType, ct);

        var registro = new MediaRegistro
        {
            StorageKey = storageKey,
            NombreOriginal = Path.GetFileName(request.NombreOriginal),
            ContentType = tipo.Value.ContentType,
            TamanoBytes = tamano,
            CreadoEn = ahora
        };

        long id;
        try
        {
            id = await media.CrearAsync(registro, ct);
        }
        catch
        {
            // No dejar el objeto huérfano en el bucket si falla el registro.
            await almacenamiento.EliminarAsync(storageKey, CancellationToken.None);
            throw;
        }

        return Result.Success(new MediaDetalleDto(
            id, almacenamiento.ConstruirUrlPublica(storageKey), registro.NombreOriginal,
            registro.ContentType, registro.TamanoBytes, registro.CreadoEn));
    }
}

// ─── Listar ───────────────────────────────────────────────────────────────────

public sealed record ListarMediaQuery(int Page, int PageSize) : IRequest<PagedResult<MediaDetalleDto>>;

public sealed class ListarMediaQueryHandler(IMediaRepository media, IAlmacenamientoObjetos almacenamiento)
    : IRequestHandler<ListarMediaQuery, PagedResult<MediaDetalleDto>>
{
    public async Task<PagedResult<MediaDetalleDto>> Handle(ListarMediaQuery request, CancellationToken ct)
    {
        var p = new PaginationParams(request.Page, request.PageSize).WithClamp();
        var (items, total) = await media.ListarAsync(p, ct);

        return new PagedResult<MediaDetalleDto>(
            items.Select(m => new MediaDetalleDto(
                m.Id, almacenamiento.ConstruirUrlPublica(m.StorageKey), m.NombreOriginal,
                m.ContentType, m.TamanoBytes, m.CreadoEn)).ToList(),
            p.Page, p.PageSize, total);
    }
}

// ─── Eliminar ─────────────────────────────────────────────────────────────────

public sealed record EliminarMediaCommand(long Id) : IRequest<Result>;

public sealed class EliminarMediaCommandHandler(IMediaRepository media, IAlmacenamientoObjetos almacenamiento)
    : IRequestHandler<EliminarMediaCommand, Result>
{
    public async Task<Result> Handle(EliminarMediaCommand request, CancellationToken ct)
    {
        var registro = await media.ObtenerAsync(request.Id, ct);
        if (registro is null)
        {
            return Result.Failure("La imagen no existe.", ErrorTipo.NoEncontrado);
        }

        if (await media.EstaEnUsoAsync(request.Id, ct))
        {
            return Result.Failure("La imagen está en uso (proyecto o sección del sitio). Quítala primero.", ErrorTipo.Conflicto);
        }

        if (!await media.EliminarAsync(request.Id, ct))
        {
            return Result.Failure("La imagen no existe.", ErrorTipo.NoEncontrado);
        }

        await almacenamiento.EliminarAsync(registro.StorageKey, ct);
        return Result.Success();
    }
}
