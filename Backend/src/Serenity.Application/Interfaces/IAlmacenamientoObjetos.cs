namespace Serenity.Application.Interfaces;

/// <summary>Almacenamiento de objetos compatible con S3 (MinIO en dev, S3/R2/Spaces en prod).</summary>
public interface IAlmacenamientoObjetos
{
    Task SubirAsync(string storageKey, Stream contenido, string contentType, CancellationToken ct);
    Task EliminarAsync(string storageKey, CancellationToken ct);
    string ConstruirUrlPublica(string storageKey);
}
