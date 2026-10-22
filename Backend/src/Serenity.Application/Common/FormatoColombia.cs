using System.Globalization;

namespace Serenity.Application.Common;

/// <summary>Fecha de hoy y formatos para textos dirigidos a personas (correos). Sin depender de la cultura instalada.</summary>
public static class FormatoColombia
{
    private static readonly string[] Meses =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    /// <summary>Colombia no tiene horario de verano: UTC-5 fijo (evita depender de tzdata en el contenedor).</summary>
    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime.AddHours(-5));

    /// <summary>$1.250.000</summary>
    public static string Pesos(long valor) => "$" + valor.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');

    /// <summary>viernes 15 de enero de 2027</summary>
    public static string Fecha(DateOnly f) => $"{Dias[(int)f.DayOfWeek]} {f.Day} de {Meses[f.Month - 1]} de {f.Year}";

    /// <summary>15/01/2027 10:30 a. m. (hora de Colombia)</summary>
    public static string FechaHora(DateTime utc)
    {
        var co = utc.ToUniversalTime().AddHours(-5);
        var h = co.Hour % 12 == 0 ? 12 : co.Hour % 12;
        return $"{co:dd/MM/yyyy} {h}:{co:mm} {(co.Hour < 12 ? "a. m." : "p. m.")}";
    }
}
