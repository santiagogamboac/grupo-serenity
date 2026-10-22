namespace Serenity.Application.Interfaces;

public sealed record MensajeCorreo(IReadOnlyList<string> Para, string Asunto, string Texto, string? ResponderA = null);

public interface IEnviadorCorreo
{
    /// <summary>false si no hay servidor SMTP configurado: los avisos solo se registran en el log.</summary>
    bool Habilitado { get; }

    /// <summary>Lanza si el servidor rechaza el mensaje; quien llama decide si eso importa.</summary>
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}
