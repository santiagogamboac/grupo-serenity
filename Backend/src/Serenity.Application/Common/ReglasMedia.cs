namespace Serenity.Application.Common;

/// <summary>Reglas de las imágenes subidas por el admin.</summary>
public static class ReglasMedia
{
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Detecta el tipo real por la firma binaria (no se confía en la extensión ni en el
    /// Content-Type del cliente). SVG queda fuera a propósito: puede llevar scripts.
    /// </summary>
    public static (string ContentType, string Extension)? DetectarTipo(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ("image/jpeg", "jpg");

        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return ("image/png", "png");

        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8'
            && (b[4] == '7' || b[4] == '9') && b[5] == 'a')
            return ("image/gif", "gif");

        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return ("image/webp", "webp");

        return null;
    }
}
