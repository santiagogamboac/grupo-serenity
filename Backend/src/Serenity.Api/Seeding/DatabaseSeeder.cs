using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Serenity.Application.Features.Contenido;
using Serenity.Application.Features.Media;
using Serenity.Application.Features.Proyectos;
using Serenity.Application.Features.Resenas;
using Serenity.Application.Features.Valores;
using Serenity.Application.Interfaces;
using Serenity.Domain;
using Serenity.Domain.Entities;

namespace Serenity.Api.Seeding;

/// <summary>
/// Datos iniciales, idempotentes: el usuario admin (desde configuración) y el contenido
/// que antes estaba hardcodeado en la landing (<c>seed/contenido.json</c> + imágenes).
/// Reutiliza los mismos comandos del panel admin, así que el seed cumple las mismas validaciones.
/// </summary>
public sealed class DatabaseSeeder(
    IConfiguration configuracion,
    IMediator mediator,
    IUsuarioRepository usuarios,
    IPasswordHasher hasher,
    IProyectoRepository proyectos,
    IResenaRepository resenas,
    IValorRepository valores,
    IEstadoAppRepository estado,
    ISeccionRepository secciones,
    ILogger<DatabaseSeeder> logger)
{
    private const string MarcaSeed = "seed_inicial_v1";

    public async Task EjecutarAsync(CancellationToken ct = default)
    {
        await SembrarAdminAsync(ct);
        await SembrarContenidoAsync(ct);
        await CompletarCamposNuevosAsync(ct);
    }

    private string RutaSeed => configuracion["Seed:Ruta"] is { Length: > 0 } rutaCfg ? rutaCfg : Path.Combine(AppContext.BaseDirectory, "seed");

    /// <summary>
    /// Bases sembradas con una versión anterior: agrega a cada sección los campos de texto que el seed tiene y la base no
    /// (p. ej. los textos de la reserva en línea). Nunca pisa un campo existente, aunque esté vacío.
    /// </summary>
    private async Task CompletarCamposNuevosAsync(CancellationToken ct)
    {
        var archivo = Path.Combine(RutaSeed, "contenido.json");
        if (!File.Exists(archivo) || !await estado.ExisteAsync(MarcaSeed, ct))
        {
            return;
        }

        var semilla = JsonNode.Parse(await File.ReadAllTextAsync(archivo, ct))!["secciones"]!.AsObject();
        foreach (var (clave, json) in await secciones.ObtenerTodasAsync(ct))
        {
            if (semilla[clave] is not JsonObject deSemilla || JsonNode.Parse(json) is not JsonObject actual)
            {
                continue;
            }

            var faltantes = deSemilla.Where(p => !p.Key.EndsWith("Archivo", StringComparison.Ordinal)
                                             && p.Value is JsonValue v && v.TryGetValue<string>(out _)
                                             && !actual.ContainsKey(p.Key)).ToList();
            if (faltantes.Count == 0)
            {
                continue;
            }

            foreach (var (nombre, valor) in faltantes)
            {
                actual[nombre] = valor!.DeepClone();
            }

            await secciones.GuardarAsync(clave, actual.ToJsonString(), ct);
            logger.LogInformation("Sección '{Clave}': se agregaron los campos nuevos {Campos}.", clave, string.Join(", ", faltantes.Select(f => f.Key)));
        }
    }

    private async Task SembrarAdminAsync(CancellationToken ct)
    {
        if (await usuarios.ContarAsync(ct) > 0)
        {
            return;
        }

        var correo = configuracion["Admin:Correo"];
        var password = configuracion["Admin:Password"];

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No hay usuarios y Admin__Correo / Admin__Password no están configurados: nadie podrá entrar al panel.");
            return;
        }

        var nombre = configuracion["Admin:Nombre"] is { Length: > 0 } n ? n : "Administrador";
        await usuarios.CrearAsync(Usuario.Crear(nombre, correo, hasher.Hash(password), Roles.Admin), ct);
        logger.LogInformation("Usuario admin inicial creado: {Correo}", correo.Trim().ToLowerInvariant());
    }

    private async Task SembrarContenidoAsync(CancellationToken ct)
    {
        if (await estado.ExisteAsync(MarcaSeed, ct))
        {
            return;
        }

        var ruta = RutaSeed;
        var archivoContenido = Path.Combine(ruta, "contenido.json");

        if (!File.Exists(archivoContenido))
        {
            logger.LogWarning("No se encontró {Archivo}: se omite el contenido inicial.", archivoContenido);
            return;
        }

        logger.LogInformation("Sembrando contenido inicial desde {Ruta}...", ruta);

        var raiz = JsonNode.Parse(await File.ReadAllTextAsync(archivoContenido, ct))!.AsObject();
        var mediaPorArchivo = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        async Task<long> SubirAsync(string archivo)
        {
            if (mediaPorArchivo.TryGetValue(archivo, out var id))
            {
                return id;
            }

            var completa = Path.Combine(ruta, "images", archivo);
            await using var stream = File.OpenRead(completa);
            var subida = await mediator.Send(new SubirMediaCommand(stream, archivo, stream.Length), ct);
            id = subida.IsSuccess ? subida.Value!.Id : throw new InvalidOperationException($"Seed: no se pudo subir '{archivo}': {subida.Error}");
            mediaPorArchivo[archivo] = id;
            return id;
        }

        // ─── Secciones: "xxxArchivo": "a.png" → "xxxMediaId": <id> ─────────────
        foreach (var (clave, valor) in raiz["secciones"]!.AsObject())
        {
            var seccion = valor!.AsObject();
            foreach (var prop in seccion.Where(p => p.Key.EndsWith("Archivo", StringComparison.Ordinal)).ToList())
            {
                seccion.Remove(prop.Key);
                seccion[prop.Key[..^"Archivo".Length] + "MediaId"] = await SubirAsync(prop.Value!.GetValue<string>());
            }

            var tipada = SeccionesRegistro.Deserializar(clave, JsonSerializer.SerializeToElement(seccion), out var error)
                         ?? throw new InvalidOperationException($"Seed: sección '{clave}' inválida: {error}");

            var guardada = await mediator.Send(new GuardarSeccionCommand(clave, tipada), ct);
            if (guardada.IsFailure)
            {
                throw new InvalidOperationException($"Seed: sección '{clave}' rechazada: {guardada.Error}");
            }
        }

        // ─── Listas: solo si la tabla está vacía ─────────────────────────────
        if ((await proyectos.ListarAsync(false, ct)).Count == 0)
        {
            foreach (var nodo in raiz["proyectos"]!.AsArray().OfType<JsonNode>())
            {
                var datos = nodo.Deserialize<ProyectoInput>(SeccionesRegistro.Json)!;
                var creado = await mediator.Send(new CrearProyectoCommand(datos), ct);
                if (creado.IsFailure)
                {
                    throw new InvalidOperationException($"Seed: proyecto '{datos.Nombre}' rechazado: {creado.Error}");
                }

                foreach (var img in nodo["imagenes"]?.AsArray().OfType<JsonNode>() ?? [])
                {
                    var mediaId = await SubirAsync(img["archivo"]!.GetValue<string>());
                    await mediator.Send(
                        new AgregarImagenProyectoCommand(creado.Value!.Id, mediaId, img["textoAlt"]?.GetValue<string>() ?? ""), ct);
                }
            }
        }

        if ((await resenas.ListarAsync(false, ct)).Count == 0)
        {
            foreach (var nodo in raiz["resenas"]!.AsArray())
            {
                var creada = await mediator.Send(new CrearResenaCommand(nodo.Deserialize<ResenaInput>(SeccionesRegistro.Json)!), ct);
                if (creada.IsFailure) throw new InvalidOperationException($"Seed: reseña rechazada: {creada.Error}");
            }
        }

        if ((await valores.ListarAsync(false, ct)).Count == 0)
        {
            foreach (var nodo in raiz["valores"]!.AsArray())
            {
                var creado = await mediator.Send(new CrearValorCommand(nodo.Deserialize<ValorInput>(SeccionesRegistro.Json)!), ct);
                if (creado.IsFailure) throw new InvalidOperationException($"Seed: valor rechazado: {creado.Error}");
            }
        }

        await estado.MarcarAsync(MarcaSeed, DateTime.UtcNow.ToString("O"), ct);
        logger.LogInformation("Contenido inicial sembrado.");
    }
}
