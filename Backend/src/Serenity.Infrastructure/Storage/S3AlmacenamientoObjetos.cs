using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Serenity.Application.Interfaces;

namespace Serenity.Infrastructure.Storage;

/// <summary>Configuración de la sección <c>Storage</c>. Vale igual para MinIO, AWS S3, R2 o Spaces.</summary>
public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Storage";

    /// <summary>Endpoint del servicio. Vacío para usar el de AWS según la región.</summary>
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "serenity-media";
    public string AccessKey { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Base pública desde la que el navegador descarga las imágenes. En compose es la ruta
    /// relativa <c>/media</c> (nginx → MinIO); en producción puede ser un CDN absoluto.
    /// </summary>
    public string UrlPublicaBase { get; set; } = "/media";
}

internal sealed class S3AlmacenamientoObjetos(IAmazonS3 s3, IOptions<OpcionesAlmacenamiento> opciones) : IAlmacenamientoObjetos
{
    private readonly OpcionesAlmacenamiento _opciones = opciones.Value;

    public async Task SubirAsync(string storageKey, Stream contenido, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opciones.Bucket,
            Key = storageKey,
            InputStream = contenido,
            ContentType = contentType,
            // Las claves llevan un GUID: el contenido nunca cambia, así que puede cachearse para siempre.
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
            // MinIO/S3 sin TLS de extremo a extremo: no se firma el payload en streaming.
            UseChunkEncoding = false
        }, ct);
    }

    public async Task EliminarAsync(string storageKey, CancellationToken ct)
        => await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _opciones.Bucket, Key = storageKey }, ct);

    public string ConstruirUrlPublica(string storageKey) => $"{_opciones.UrlPublicaBase.TrimEnd('/')}/{storageKey}";
}
