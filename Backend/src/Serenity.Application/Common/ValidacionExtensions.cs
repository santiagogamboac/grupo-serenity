using FluentValidation;

namespace Serenity.Application.Common;

public static class ValidacionExtensions
{
    /// <summary>Vacío, ancla (#x), ruta (/x) o URL http(s)/mailto/tel.</summary>
    public static bool EsEnlaceSeguro(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return true;
        var v = valor.Trim();
        if (v.StartsWith('#') || (v.StartsWith('/') && !v.StartsWith("//"))) return true;
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme is "http" or "https" or "mailto" or "tel";
    }

    public static IRuleBuilderOptions<T, string?> EnlaceSeguro<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(EsEnlaceSeguro).WithMessage("'{PropertyName}' debe ser una URL http(s), un ancla (#seccion) o una ruta (/ruta).");

    public static IRuleBuilderOptions<T, string> Texto<T>(this IRuleBuilder<T, string> rule, int max, bool requerido = true)
    {
        var r = requerido ? rule.NotEmpty() : rule;
        return r.MaximumLength(max);
    }

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty()
               .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
               .MaximumLength(128)
               .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit))
               .WithMessage("La contraseña debe incluir al menos una letra y un número.");
}
