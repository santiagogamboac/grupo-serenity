using FluentValidation;
using MediatR;
using Serenity.Application.Common;
using Serenity.Application.Features.Notificaciones;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Reservas;

public sealed record ReservaAdminDto(
    long Id, string Codigo, long AlojamientoId, string AlojamientoNombre, long ProyectoId, string ProyectoNombre,
    DateOnly Llegada, DateOnly Salida, int Noches, int Huespedes, string Nombre, string Correo, string Telefono,
    string? NotaHuesped, long Subtotal, long TarifaAseo, long Total, string Moneda, string Estado, string? MotivoEstado,
    DateTime? ExpiraEn, string? PagoProveedor, int PagoIntentos, string? PagoReferencia, string? PagoTransaccionId,
    string? PagoEstado, string? PagoMetodo, DateTime? PagadoEn, string? Notas, string EnlaceHuesped,
    DateTime CreadoEn, DateTime ActualizadoEn)
{
    internal static ReservaAdminDto De(ReservaRegistro r, OpcionesReservas o) => new(
        r.Id, r.Codigo, r.AlojamientoId, r.AlojamientoNombre, r.ProyectoId, r.ProyectoNombre, r.Llegada, r.Salida,
        r.Noches, r.Huespedes, r.Nombre, r.Correo, r.Telefono, r.NotaHuesped, r.Subtotal, r.TarifaAseo, r.Total, r.Moneda,
        r.Estado, r.MotivoEstado, r.ExpiraEn, r.PagoProveedor, r.PagoIntentos, r.PagoReferencia, r.PagoTransaccionId,
        r.PagoEstado, r.PagoMetodo, r.PagadoEn, r.Notas, o.EnlaceReserva(r.Codigo, r.TokenConsulta), r.CreadoEn, r.ActualizadoEn);
}

public sealed record ListarReservasQuery(string? Estado, string? Q, int Page, int PageSize) : IRequest<Result<PagedResult<ReservaAdminDto>>>;

public sealed class ListarReservasQueryHandler(IReservaRepository reservas, OpcionesReservas opciones)
    : IRequestHandler<ListarReservasQuery, Result<PagedResult<ReservaAdminDto>>>
{
    public async Task<Result<PagedResult<ReservaAdminDto>>> Handle(ListarReservasQuery request, CancellationToken ct)
    {
        var estado = string.IsNullOrWhiteSpace(request.Estado) ? null : request.Estado.Trim().ToLowerInvariant();
        if (estado is not null && !EstadosReserva.Todos.Contains(estado))
        {
            return Result.Failure<PagedResult<ReservaAdminDto>>("Estado no válido.");
        }

        await reservas.ExpirarVencidasAsync(ct);
        var p = new PaginationParams(request.Page, request.PageSize).WithClamp();
        var q = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();
        var (items, total) = await reservas.ListarAsync(estado, q, p, ct);

        return Result.Success(new PagedResult<ReservaAdminDto>(items.Select(r => ReservaAdminDto.De(r, opciones)).ToList(), p.Page, p.PageSize, total));
    }
}

public sealed record ObtenerReservaQuery(long Id) : IRequest<Result<ReservaAdminDto>>;

public sealed class ObtenerReservaQueryHandler(IReservaRepository reservas, OpcionesReservas opciones)
    : IRequestHandler<ObtenerReservaQuery, Result<ReservaAdminDto>>
{
    public async Task<Result<ReservaAdminDto>> Handle(ObtenerReservaQuery request, CancellationToken ct)
    {
        var r = await reservas.ObtenerAsync(request.Id, ct);
        return r is null
            ? Result.Failure<ReservaAdminDto>("La reserva no existe.", ErrorTipo.NoEncontrado)
            : Result.Success(ReservaAdminDto.De(r, opciones));
    }
}

/// <summary>Confirmación manual: el pago llegó por otro medio (transferencia, efectivo) o se resolvió una revisión.</summary>
public sealed record ConfirmarReservaCommand(long Id) : IRequest<Result<ReservaAdminDto>>;

public sealed class ConfirmarReservaCommandHandler(IReservaRepository reservas, Notificador notificador, OpcionesReservas opciones)
    : IRequestHandler<ConfirmarReservaCommand, Result<ReservaAdminDto>>
{
    public async Task<Result<ReservaAdminDto>> Handle(ConfirmarReservaCommand request, CancellationToken ct)
    {
        var r = await reservas.ObtenerAsync(request.Id, ct);
        if (r is null)
        {
            return Result.Failure<ReservaAdminDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        if (r.Estado == EstadosReserva.Confirmada)
        {
            return Result.Failure<ReservaAdminDto>("La reserva ya está confirmada.", ErrorTipo.Conflicto);
        }

        // Si Wompi ya cobró (p. ej. una revisión resuelta), se conserva ese proveedor.
        var proveedor = r.PagoEstado == ProcesadorPagos.Aprobada ? "wompi" : "manual";
        switch (await reservas.ConfirmarSiDisponibleAsync(r.Id, proveedor, null, ct))
        {
            case ResultadoConfirmacion.FechasOcupadas:
                return Result.Failure<ReservaAdminDto>("No se puede confirmar: esas noches ya están ocupadas por otra reserva o un bloqueo.", ErrorTipo.Conflicto);
            case ResultadoConfirmacion.NoEncontrada:
                return Result.Failure<ReservaAdminDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        var confirmada = (await reservas.ObtenerAsync(r.Id, ct))!;
        await notificador.ReservaConfirmadaAsync(confirmada, ct);
        return Result.Success(ReservaAdminDto.De(confirmada, opciones));
    }
}

public sealed record CancelarReservaInput(string Motivo, bool AvisarHuesped = true);

public sealed record CancelarReservaCommand(long Id, string Motivo, bool AvisarHuesped) : IRequest<Result<ReservaAdminDto>>;

public sealed class CancelarReservaCommandValidator : AbstractValidator<CancelarReservaCommand>
{
    public CancelarReservaCommandValidator() => RuleFor(x => x.Motivo).Texto(300);
}

public sealed class CancelarReservaCommandHandler(IReservaRepository reservas, Notificador notificador, OpcionesReservas opciones)
    : IRequestHandler<CancelarReservaCommand, Result<ReservaAdminDto>>
{
    public async Task<Result<ReservaAdminDto>> Handle(CancelarReservaCommand request, CancellationToken ct)
    {
        var r = await reservas.ObtenerAsync(request.Id, ct);
        if (r is null)
        {
            return Result.Failure<ReservaAdminDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        if (r.Estado is EstadosReserva.Cancelada or EstadosReserva.Expirada)
        {
            return Result.Failure<ReservaAdminDto>("La reserva ya no está activa.", ErrorTipo.Conflicto);
        }

        var motivo = request.Motivo.Trim();
        await reservas.CambiarEstadoAsync(r.Id, EstadosReserva.Cancelada, motivo, ct);
        var cancelada = (await reservas.ObtenerAsync(r.Id, ct))!;
        if (request.AvisarHuesped)
        {
            await notificador.ReservaCanceladaAsync(cancelada, motivo, ct);
        }

        return Result.Success(ReservaAdminDto.De(cancelada, opciones));
    }
}

public sealed record NotasReservaInput(string? Notas);

public sealed record ActualizarNotasReservaCommand(long Id, string? Notas) : IRequest<Result<ReservaAdminDto>>;

public sealed class ActualizarNotasReservaCommandValidator : AbstractValidator<ActualizarNotasReservaCommand>
{
    public ActualizarNotasReservaCommandValidator() => RuleFor(x => x.Notas).MaximumLength(4000);
}

public sealed class ActualizarNotasReservaCommandHandler(IReservaRepository reservas, OpcionesReservas opciones)
    : IRequestHandler<ActualizarNotasReservaCommand, Result<ReservaAdminDto>>
{
    public async Task<Result<ReservaAdminDto>> Handle(ActualizarNotasReservaCommand request, CancellationToken ct)
    {
        var notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        if (!await reservas.ActualizarNotasAsync(request.Id, notas, ct))
        {
            return Result.Failure<ReservaAdminDto>("La reserva no existe.", ErrorTipo.NoEncontrado);
        }

        return Result.Success(ReservaAdminDto.De((await reservas.ObtenerAsync(request.Id, ct))!, opciones));
    }
}
