import { useEffect, useState } from "react";
import { Link, useLocation } from "wouter";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { ArrowLeft, ImageIcon, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { mensajeDeError } from "@/lib/api";
import {
  useActualizarProyecto,
  useAgregarImagenProyecto,
  useAltImagenProyecto,
  useCatalogos,
  useCrearProyecto,
  useEliminarImagenProyecto,
  useProyecto,
  useReordenarImagenesProyecto,
} from "../api";
import { BotonEliminar, Campo, EncabezadoPagina, moverId, Ordenar, SelectorImagen, SelectNativo } from "../componentes";
import type { ProyectoAdmin, ProyectoImagenAdmin } from "../tipos";

const enlaceSeguro = (v: string | null | undefined) => {
  if (!v) return true;
  return /^(#|\/(?!\/)|https?:\/\/|mailto:|tel:)/i.test(v.trim());
};

const esquema = z.object({
  nombre: z.string().trim().min(1, "Requerido.").max(120),
  etiqueta: z.string().trim().min(1, "Requerido.").max(40),
  icono: z.string().min(1, "Elige un icono."),
  ubicacionEtiqueta: z.string().trim().min(1, "Requerido.").max(120),
  titulo: z.string().trim().min(1, "Requerido.").max(120),
  descripcion: z.string().trim().min(1, "Requerido.").max(800),
  metaLugar: z.string().trim().max(120),
  metaEstancia: z.string().trim().max(120),
  urlReserva: z.string().trim().max(500).refine(enlaceSeguro, "Debe empezar por https://, http://, # o /."),
  ctaTexto: z.string().trim().min(1, "Requerido.").max(60),
  publicado: z.boolean(),
});
type Valores = z.infer<typeof esquema>;

const VACIO: Valores = {
  nombre: "", etiqueta: "", icono: "sun", ubicacionEtiqueta: "", titulo: "", descripcion: "",
  metaLugar: "", metaEstancia: "", urlReserva: "", ctaTexto: "Ver alojamientos", publicado: true,
};

export function ProyectoFormPage({ id }: { id: number | null }) {
  const [, navegar] = useLocation();
  const { data: proyecto, isPending, isError } = useProyecto(id);
  const { data: catalogos } = useCatalogos();
  const crear = useCrearProyecto();
  const actualizar = useActualizarProyecto();

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<Valores>({ resolver: zodResolver(esquema), defaultValues: VACIO });

  // Carga los datos del proyecto en el formulario al llegar (o al cambiar de proyecto).
  useEffect(() => {
    if (proyecto) reset({ ...proyecto, urlReserva: proyecto.urlReserva ?? "" });
  }, [proyecto, reset]);

  const guardar = handleSubmit(async v => {
    const datos = { ...v, urlReserva: v.urlReserva || null };
    try {
      if (id === null) {
        const creado = await crear.mutateAsync(datos);
        toast.success("Proyecto creado. Ahora agrégale imágenes.");
        navegar(`/admin/proyectos/${creado.id}`);
      } else {
        const actualizado = await actualizar.mutateAsync({ id, datos });
        toast.success("Proyecto guardado");
        reset({ ...actualizado, urlReserva: actualizado.urlReserva ?? "" });
      }
    } catch (e) {
      // useMutacion ya muestra el toast de error.
      void e;
    }
  });

  if (id !== null && isPending) return <Skeleton className="h-96" />;
  if (id !== null && (isError || !proyecto)) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-muted-foreground">No encontramos ese proyecto.</p>
        <Button asChild variant="outline">
          <Link href="/admin/proyectos">Volver a proyectos</Link>
        </Button>
      </div>
    );
  }

  return (
    <>
      <EncabezadoPagina
        titulo={id === null ? "Nuevo proyecto" : proyecto!.nombre}
        descripcion={id === null ? "Completa los datos; las imágenes se agregan justo después de crearlo." : `Dirección pública: ${proyecto!.slug}`}
        acciones={
          <Button asChild variant="outline">
            <Link href="/admin/proyectos">
              <ArrowLeft /> Proyectos
            </Link>
          </Button>
        }
      />

      <form onSubmit={guardar} noValidate className="space-y-5 rounded-lg border bg-card p-5" aria-label="Datos del proyecto">
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Nombre" htmlFor="nombre" error={errors.nombre?.message} ayuda="Se usa en la lista del admin y en el select del formulario de reservas.">
            <Input id="nombre" {...register("nombre")} />
          </Campo>
          <Campo etiqueta="Ubicación (etiqueta)" htmlFor="ubicacionEtiqueta" error={errors.ubicacionEtiqueta?.message} ayuda="Ej.: Santa Marta · Magdalena">
            <Input id="ubicacionEtiqueta" {...register("ubicacionEtiqueta")} />
          </Campo>
          <Campo etiqueta="Texto de la pestaña" htmlFor="etiqueta" error={errors.etiqueta?.message} ayuda="Ej.: Playa">
            <Input id="etiqueta" {...register("etiqueta")} />
          </Campo>
          <Campo etiqueta="Icono de la pestaña" htmlFor="icono" error={errors.icono?.message}>
            <SelectNativo id="icono" {...register("icono")}>
              {(catalogos?.iconos ?? [VACIO.icono]).map(i => (
                <option key={i} value={i}>
                  {i}
                </option>
              ))}
            </SelectNativo>
          </Campo>
        </div>
        <Campo etiqueta="Título" htmlFor="titulo" error={errors.titulo?.message} ayuda="El titular grande del destino. Ej.: El mar cerca.">
          <Input id="titulo" {...register("titulo")} />
        </Campo>
        <Campo etiqueta="Descripción" htmlFor="descripcion" error={errors.descripcion?.message}>
          <Textarea id="descripcion" rows={3} {...register("descripcion")} />
        </Campo>
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Lugar (dato secundario)" htmlFor="metaLugar" error={errors.metaLugar?.message} ayuda="Ej.: Reserva del Mar">
            <Input id="metaLugar" {...register("metaLugar")} />
          </Campo>
          <Campo etiqueta="Estancia (dato secundario)" htmlFor="metaEstancia" error={errors.metaEstancia?.message} ayuda="Ej.: Escapadas desde 2 noches">
            <Input id="metaEstancia" {...register("metaEstancia")} />
          </Campo>
          <Campo etiqueta="Enlace del botón" htmlFor="urlReserva" error={errors.urlReserva?.message} ayuda="Vacío = el botón no se muestra.">
            <Input id="urlReserva" placeholder="https://…" {...register("urlReserva")} />
          </Campo>
          <Campo etiqueta="Texto del botón" htmlFor="ctaTexto" error={errors.ctaTexto?.message}>
            <Input id="ctaTexto" {...register("ctaTexto")} />
          </Campo>
        </div>
        <label className="flex items-center gap-3 text-sm">
          <Switch checked={watch("publicado")} onCheckedChange={v => setValue("publicado", v, { shouldDirty: true })} aria-label="Publicado" />
          Publicado (visible en el sitio)
        </label>

        <div className="flex items-center gap-3 border-t pt-4">
          <Button type="submit" disabled={isSubmitting || (id !== null && !isDirty)}>
            {isSubmitting ? "Guardando…" : id === null ? "Crear proyecto" : "Guardar cambios"}
          </Button>
          {id !== null && !isDirty && <span className="text-xs text-muted-foreground">Sin cambios por guardar.</span>}
        </div>
      </form>

      {proyecto && <Galeria proyecto={proyecto} />}
    </>
  );
}

// ─── Galería ──────────────────────────────────────────────────────────────────

function Galeria({ proyecto }: { proyecto: ProyectoAdmin }) {
  const [selector, setSelector] = useState(false);
  const agregar = useAgregarImagenProyecto();
  const quitar = useEliminarImagenProyecto();
  const reordenar = useReordenarImagenesProyecto();
  const imagenes = proyecto.imagenes;

  return (
    <section className="mt-8" aria-label="Galería del proyecto">
      <div className="mb-3 flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">Imágenes</h2>
          <p className="text-sm text-muted-foreground">
            La <strong>primera</strong> es la foto grande del destino y la <strong>segunda</strong> la de la franja inferior.
          </p>
        </div>
        <Button variant="outline" onClick={() => setSelector(true)}>
          <Plus /> Agregar imagen
        </Button>
      </div>

      {imagenes.length === 0 && (
        <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
          Este proyecto aún no tiene imágenes.
        </p>
      )}

      <ul className="space-y-3">
        {imagenes.map((img, i) => (
          <FilaImagen
            key={img.id}
            proyectoId={proyecto.id}
            img={img}
            indice={i}
            total={imagenes.length}
            ocupado={reordenar.isPending}
            onMover={d => reordenar.mutate({ id: proyecto.id, ids: moverId(imagenes, i, d) })}
            onQuitar={() => quitar.mutate({ id: proyecto.id, imagenId: img.id })}
          />
        ))}
      </ul>

      <SelectorImagen
        abierto={selector}
        onCerrar={() => setSelector(false)}
        onElegir={m =>
          agregar.mutate(
            { id: proyecto.id, mediaId: m.id, textoAlt: "" },
            { onError: e => toast.error(mensajeDeError(e)) }
          )
        }
      />
    </section>
  );
}

function FilaImagen({
  proyectoId,
  img,
  indice,
  total,
  ocupado,
  onMover,
  onQuitar,
}: {
  proyectoId: number;
  img: ProyectoImagenAdmin;
  indice: number;
  total: number;
  ocupado: boolean;
  onMover: (d: -1 | 1) => void;
  onQuitar: () => void;
}) {
  const [alt, setAlt] = useState(img.textoAlt);
  const guardarAlt = useAltImagenProyecto();
  useEffect(() => setAlt(img.textoAlt), [img.textoAlt]);

  return (
    <li className="flex items-center gap-3 rounded-lg border bg-card p-3">
      <Ordenar indice={indice} total={total} deshabilitado={ocupado} onMover={onMover} />
      <div className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted">
        {img.url ? <img src={img.url} alt={img.textoAlt} className="size-full object-cover" /> : <ImageIcon />}
      </div>
      <div className="min-w-0 flex-1">
        <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
          {indice === 0 ? "Foto principal" : indice === 1 ? "Franja inferior" : `Imagen ${indice + 1}`}
        </span>
        <Input
          value={alt}
          maxLength={200}
          placeholder="Texto alternativo (describe la imagen)"
          aria-label="Texto alternativo"
          onChange={e => setAlt(e.target.value)}
          onBlur={() => alt !== img.textoAlt && guardarAlt.mutate({ id: proyectoId, imagenId: img.id, textoAlt: alt })}
        />
      </div>
      <BotonEliminar onClick={onQuitar} etiqueta="Quitar del proyecto" />
    </li>
  );
}
