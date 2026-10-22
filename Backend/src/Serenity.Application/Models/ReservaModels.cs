namespace Serenity.Application.Models;

public sealed class AlojamientoDatos
{
    public string Nombre { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public long? ImagenMediaId { get; init; }
    public int Capacidad { get; init; }
    public int HuespedesIncluidos { get; init; }
    public long PrecioNoche { get; init; }
    public long RecargoHuespedExtra { get; init; }
    public long TarifaAseo { get; init; }
    public int EstanciaMinima { get; init; }
    public long? AnuncioId { get; init; }
    public bool Publicado { get; init; }
}

public sealed class AlojamientoRegistro
{
    public long Id { get; init; }
    public long ProyectoId { get; init; }
    public string Nombre { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public long? ImagenMediaId { get; init; }
    public int Capacidad { get; init; }
    public int HuespedesIncluidos { get; init; }
    public long PrecioNoche { get; init; }
    public long RecargoHuespedExtra { get; init; }
    public long TarifaAseo { get; init; }
    public int EstanciaMinima { get; init; }
    public long? AnuncioId { get; init; }
    public string TokenIcal { get; init; } = default!;
    public int Orden { get; init; }
    public bool Publicado { get; init; }
}

public sealed class TarifaRegistro
{
    public long Id { get; init; }
    public long AlojamientoId { get; init; }
    public string Nombre { get; init; } = default!;
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public long PrecioNoche { get; init; }
}

public sealed class BloqueoManualRegistro
{
    public long Id { get; init; }
    public long AlojamientoId { get; init; }
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public string Motivo { get; init; } = default!;
    public DateTime CreadoEn { get; init; }
}

/// <summary>Noches ocupadas de un alojamiento con su procedencia (para el calendario y el feed .ics).</summary>
public sealed class OcupacionRegistro
{
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }

    /// <summary>reserva · manual · airbnb.</summary>
    public string Origen { get; init; } = default!;

    /// <summary>Código de la reserva o id del bloqueo: identificador estable para el UID del .ics.</summary>
    public string Referencia { get; init; } = default!;
}

public sealed class ReservaNueva
{
    public string Codigo { get; init; } = default!;
    public string TokenConsulta { get; init; } = default!;
    public long AlojamientoId { get; init; }
    public DateOnly Llegada { get; init; }
    public DateOnly Salida { get; init; }
    public int Huespedes { get; init; }
    public string Nombre { get; init; } = default!;
    public string Correo { get; init; } = default!;
    public string Telefono { get; init; } = default!;
    public string? NotaHuesped { get; init; }
    public int Noches { get; init; }
    public long Subtotal { get; init; }
    public long TarifaAseo { get; init; }
    public long Total { get; init; }
    public DateTime ExpiraEn { get; init; }
}

public sealed class ReservaRegistro
{
    public long Id { get; init; }
    public string Codigo { get; init; } = default!;
    public string TokenConsulta { get; init; } = default!;
    public long AlojamientoId { get; init; }
    public string AlojamientoNombre { get; init; } = default!;
    public long ProyectoId { get; init; }
    public string ProyectoNombre { get; init; } = default!;
    public DateOnly Llegada { get; init; }
    public DateOnly Salida { get; init; }
    public int Huespedes { get; init; }
    public string Nombre { get; init; } = default!;
    public string Correo { get; init; } = default!;
    public string Telefono { get; init; } = default!;
    public string? NotaHuesped { get; init; }
    public int Noches { get; init; }
    public long Subtotal { get; init; }
    public long TarifaAseo { get; init; }
    public long Total { get; init; }
    public string Moneda { get; init; } = default!;
    public string Estado { get; init; } = default!;
    public string? MotivoEstado { get; init; }
    public DateTime? ExpiraEn { get; init; }
    public string? PagoProveedor { get; init; }
    public int PagoIntentos { get; init; }
    public string? PagoReferencia { get; init; }
    public string? PagoTransaccionId { get; init; }
    public string? PagoEstado { get; init; }
    public string? PagoMetodo { get; init; }
    public DateTime? PagadoEn { get; init; }
    public string? Notas { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ActualizadoEn { get; init; }
}

/// <summary>Resultado de aplicar una transacción de pago a una reserva.</summary>
public sealed record TransaccionPago(
    string Id, string Referencia, string Estado, long MontoCentavos, string Moneda, string? Metodo);
