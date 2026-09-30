import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { AlertTriangle, CheckCircle2, ExternalLink, Pencil, Plus, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import {
  useActualizarAnuncio,
  useAnuncios,
  useCrearAnuncio,
  useEliminarAnuncio,
  useSincronizarAnuncio,
} from "../api";
import { BotonEliminar, Campo, ConfirmarEliminar, formatoFecha } from "../componentes";
import type { AnuncioAdmin } from "../tipos";

const HOST_AIRBNB = /^(www\.)?airbnb\.[a-z]{2,3}(\.[a-z]{2})?$/i;

const parseUrl = (v: string) => {
  try {
    return new URL(v.trim());
  } catch {
    return null;
  }
};
const idAnuncio = (v: string) => {
  const u = parseUrl(v);
  return u && u.protocol === "https:" && HOST_AIRBNB.test(u.hostname) ? (u.pathname.match(/^\/rooms\/(\d{1,25})\/?$/i)?.[1] ?? null) : null;
};
const idIcal = (v: string) => parseUrl(v)?.pathname.match(/^\/calendar\/ical\/(\d{1,25})\.ics$/i)?.[1] ?? null;

const esquema = z
  .object({
    nombre: z.string().trim().min(1, "Requerido.").max(120),
    urlAnuncio: z
      .string()
      .trim()
      .max(500)
      .refine(v => idAnuncio(v) !== null, "Pega la URL del anuncio de Airbnb, ej.: https://www.airbnb.com/rooms/123456"),
    icalUrl: z
      .string()
      .trim()
      .max(600)
      .refine(v => {
        if (!v) return true;
        const u = parseUrl(v);
        return !!u && u.protocol === "https:" && HOST_AIRBNB.test(u.hostname) && /\.ics/i.test(v);
      }, "Debe ser https, de Airbnb, y terminar en .ics"),
    publicado: z.boolean(),
  })
  .superRefine((d, ctx) => {
    const ical = d.icalUrl && idIcal(d.icalUrl);
    if (ical && idAnuncio(d.urlAnuncio) && ical !== idAnuncio(d.urlAnuncio)) {
      ctx.addIssue({
        code: "custom",
        path: ["icalUrl"],
        message: "Este calendario es de otro anuncio: su id no coincide con el de la URL del anuncio.",
      });
    }
  });
type Valores = z.infer<typeof esquema>;

const VACIO: Valores = { nombre: "", urlAnuncio: "", icalUrl: "", publicado: true };

export function AnunciosProyecto({ proyectoId }: { proyectoId: number }) {
  const { data: anuncios, isPending, isError } = useAnuncios(proyectoId);
  const [editando, setEditando] = useState<AnuncioAdmin | "nuevo" | null>(null);

  return (
    <section className="mt-8" aria-label="Anuncios de Airbnb">
      <div className="mb-3 flex items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold">Anuncios de Airbnb</h2>
          <p className="text-sm text-muted-foreground">
            Cada anuncio tiene su propio calendario. Con el enlace iCal, el sitio marca como ocupadas las fechas que Airbnb bloquea.
          </p>
        </div>
        {editando === null && (
          <Button variant="outline" onClick={() => setEditando("nuevo")}>
            <Plus /> Agregar anuncio
          </Button>
        )}
      </div>

      {editando !== null && (
        <FormularioAnuncio
          key={editando === "nuevo" ? "nuevo" : editando.id}
          proyectoId={proyectoId}
          anuncio={editando === "nuevo" ? null : editando}
          onCerrar={() => setEditando(null)}
        />
      )}

      {isPending && <Skeleton className="h-24" />}
      {isError && <p className="text-sm text-destructive">No se pudieron cargar los anuncios.</p>}
      {anuncios?.length === 0 && editando === null && (
        <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          Este proyecto aún no tiene anuncios de Airbnb vinculados.
        </p>
      )}
      <ul className="space-y-3">
        {anuncios?.map(a => (
          <FilaAnuncio key={a.id} anuncio={a} onEditar={() => setEditando(a)} />
        ))}
      </ul>
    </section>
  );
}

function FilaAnuncio({ anuncio, onEditar }: { anuncio: AnuncioAdmin; onEditar: () => void }) {
  const sincronizar = useSincronizarAnuncio();
  const eliminar = useEliminarAnuncio();
  const [confirmar, setConfirmar] = useState(false);
  const conIcal = anuncio.icalUrl !== null;

  return (
    <li className="rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="font-medium">{anuncio.nombre}</h3>
            {!anuncio.publicado && <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">Oculto</span>}
          </div>
          <a
            href={anuncio.urlAnuncio}
            target="_blank"
            rel="noreferrer noopener"
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:underline"
          >
            Airbnb #{anuncio.airbnbRoomId} <ExternalLink className="size-3" />
          </a>
        </div>
        <div className="flex items-center gap-1">
          {conIcal && (
            <Button
              variant="outline"
              size="sm"
              disabled={sincronizar.isPending}
              onClick={() =>
                sincronizar.mutate(anuncio.id, {
                  onSuccess: r => (r.ultimaSyncError ? toast.error("No se pudo sincronizar: " + r.ultimaSyncError) : toast.success("Calendario sincronizado")),
                })
              }
            >
              <RefreshCw className={sincronizar.isPending ? "animate-spin" : ""} /> {sincronizar.isPending ? "Sincronizando…" : "Sincronizar ahora"}
            </Button>
          )}
          <Button variant="ghost" size="icon-sm" aria-label="Editar anuncio" title="Editar" onClick={onEditar}>
            <Pencil />
          </Button>
          <BotonEliminar onClick={() => setConfirmar(true)} etiqueta="Eliminar anuncio" />
        </div>
      </div>

      <EstadoSync anuncio={anuncio} />

      <ConfirmarEliminar
        abierto={confirmar}
        onCerrar={() => setConfirmar(false)}
        onConfirmar={() => eliminar.mutate(anuncio.id)}
        titulo={`¿Eliminar «${anuncio.nombre}»?`}
        descripcion="Se borra el vínculo y sus fechas bloqueadas en el sitio. No afecta al anuncio en Airbnb."
      />
    </li>
  );
}

function EstadoSync({ anuncio }: { anuncio: AnuncioAdmin }) {
  if (anuncio.icalUrl === null) {
    return <p className="mt-2 text-xs text-muted-foreground">Sin enlace iCal: el sitio no conoce las fechas ocupadas de este anuncio.</p>;
  }
  return (
    <div className="mt-2 space-y-1 text-xs">
      {anuncio.ultimaSyncOk ? (
        <p className="flex items-center gap-1.5 text-muted-foreground">
          <CheckCircle2 className="size-3.5 text-green-600" /> Última sincronización correcta: {formatoFecha(anuncio.ultimaSyncOk)}
        </p>
      ) : (
        <p className="text-muted-foreground">Aún no se ha sincronizado con éxito.</p>
      )}
      {anuncio.ultimaSyncError && (
        <p className="flex items-start gap-1.5 text-destructive" role="alert">
          <AlertTriangle className="mt-0.5 size-3.5 shrink-0" />
          <span>
            Falló el último intento{anuncio.ultimaSyncIntento ? ` (${formatoFecha(anuncio.ultimaSyncIntento)})` : ""}: {anuncio.ultimaSyncError}
            {anuncio.ultimaSyncOk && " Se conservan las fechas de la última sincronización correcta."}
          </span>
        </p>
      )}
    </div>
  );
}

function FormularioAnuncio({
  proyectoId,
  anuncio,
  onCerrar,
}: {
  proyectoId: number;
  anuncio: AnuncioAdmin | null;
  onCerrar: () => void;
}) {
  const crear = useCrearAnuncio();
  const actualizar = useActualizarAnuncio();
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({
    resolver: zodResolver(esquema),
    defaultValues: anuncio ? { nombre: anuncio.nombre, urlAnuncio: anuncio.urlAnuncio, icalUrl: anuncio.icalUrl ?? "", publicado: anuncio.publicado } : VACIO,
  });

  const guardar = handleSubmit(async v => {
    const datos = { ...v, icalUrl: v.icalUrl || null };
    try {
      const r = anuncio
        ? await actualizar.mutateAsync({ id: anuncio.id, datos })
        : await crear.mutateAsync({ proyectoId, datos });
      if (r.ultimaSyncError) toast.warning("Anuncio guardado, pero el calendario no se pudo leer: " + r.ultimaSyncError);
      else toast.success(anuncio ? "Anuncio guardado" : "Anuncio agregado");
      onCerrar();
    } catch (e) {
      // useMutacion ya muestra el toast de error.
      void e;
    }
  });

  return (
    <form onSubmit={guardar} noValidate className="mb-4 space-y-4 rounded-lg border bg-card p-5" aria-label={anuncio ? "Editar anuncio" : "Nuevo anuncio"}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo etiqueta="Nombre" htmlFor="anuncio-nombre" error={errors.nombre?.message} ayuda="Solo para identificarlo. Ej.: Cabaña del lago">
          <Input id="anuncio-nombre" {...register("nombre")} />
        </Campo>
        <Campo etiqueta="URL del anuncio en Airbnb" htmlFor="anuncio-url" error={errors.urlAnuncio?.message} ayuda="Con ella se identifica a qué anuncio pertenece el calendario.">
          <Input id="anuncio-url" placeholder="https://www.airbnb.com.co/rooms/123456" {...register("urlAnuncio")} />
        </Campo>
      </div>
      <Campo
        etiqueta="Enlace iCal (opcional)"
        htmlFor="anuncio-ical"
        error={errors.icalUrl?.message}
        ayuda="Airbnb → Calendario → elige el anuncio → Disponibilidad → Sincronizar calendarios → Exportar calendario. Es privado: no se muestra en el sitio."
      >
        <Input id="anuncio-ical" autoComplete="off" placeholder="https://www.airbnb.com/calendar/ical/123456.ics?s=…" {...register("icalUrl")} />
      </Campo>
      <label className="flex items-center gap-3 text-sm">
        <Switch checked={watch("publicado")} onCheckedChange={v => setValue("publicado", v, { shouldDirty: true })} aria-label="Publicado" />
        Activo (se sincroniza y cuenta para la disponibilidad)
      </label>
      <div className="flex items-center gap-3 border-t pt-4">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Guardando…" : anuncio ? "Guardar anuncio" : "Agregar anuncio"}
        </Button>
        <Button type="button" variant="ghost" onClick={onCerrar}>
          Cancelar
        </Button>
      </div>
    </form>
  );
}
