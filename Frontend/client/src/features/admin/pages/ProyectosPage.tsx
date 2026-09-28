import { useState } from "react";
import { Link } from "wouter";
import { ImageIcon, Pencil, Plus } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { useActualizarProyecto, useEliminarProyecto, useProyectos, useReordenarProyectos } from "../api";
import { BotonEliminar, ConfirmarEliminar, EncabezadoPagina, moverId, Ordenar } from "../componentes";
import type { ProyectoAdmin } from "../tipos";

export function ProyectosPage() {
  const { data: proyectos, isPending } = useProyectos();
  const actualizar = useActualizarProyecto();
  const eliminar = useEliminarProyecto();
  const reordenar = useReordenarProyectos();
  const [porBorrar, setPorBorrar] = useState<ProyectoAdmin | null>(null);

  const alternarPublicado = (p: ProyectoAdmin) =>
    actualizar.mutate({
      id: p.id,
      datos: {
        nombre: p.nombre, etiqueta: p.etiqueta, icono: p.icono, ubicacionEtiqueta: p.ubicacionEtiqueta,
        titulo: p.titulo, descripcion: p.descripcion, metaLugar: p.metaLugar, metaEstancia: p.metaEstancia,
        urlReserva: p.urlReserva, ctaTexto: p.ctaTexto, publicado: !p.publicado,
      },
    });

  return (
    <>
      <EncabezadoPagina
        titulo="Proyectos"
        descripcion="Cada proyecto es un destino de la sección “Elige tu escenario”. El orden de esta lista es el de las pestañas del sitio."
        acciones={
          <Button asChild>
            <Link href="/admin/proyectos/nuevo">
              <Plus /> Nuevo proyecto
            </Link>
          </Button>
        }
      />

      {isPending && <Skeleton className="h-32" />}
      {proyectos?.length === 0 && (
        <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
          Aún no hay proyectos. Crea el primero para que aparezca en el sitio.
        </p>
      )}

      <ul className="space-y-3">
        {proyectos?.map((p, i) => (
          <li key={p.id} className="flex items-center gap-3 rounded-lg border bg-card p-3" data-testid={`proyecto-${p.id}`}>
            <Ordenar
              indice={i}
              total={proyectos.length}
              deshabilitado={reordenar.isPending}
              onMover={d => reordenar.mutate(moverId(proyectos, i, d))}
            />
            <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted">
              {p.imagenes[0] ? <img src={p.imagenes[0].url} alt="" className="size-full object-cover" /> : <ImageIcon className="text-muted-foreground" />}
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="truncate font-medium">{p.nombre}</span>
                <Badge variant="secondary">{p.etiqueta}</Badge>
                {!p.publicado && <Badge variant="outline">Borrador</Badge>}
              </div>
              <p className="truncate text-sm text-muted-foreground">
                {p.ubicacionEtiqueta} · {p.imagenes.length} imagen(es)
              </p>
            </div>
            <label className="flex items-center gap-2 text-xs text-muted-foreground">
              <Switch checked={p.publicado} onCheckedChange={() => alternarPublicado(p)} disabled={actualizar.isPending} aria-label={`Publicar ${p.nombre}`} />
              <span className="hidden sm:inline">Publicado</span>
            </label>
            <Button asChild variant="outline" size="icon-sm" aria-label={`Editar ${p.nombre}`} title="Editar">
              <Link href={`/admin/proyectos/${p.id}`}>
                <Pencil />
              </Link>
            </Button>
            <BotonEliminar onClick={() => setPorBorrar(p)} />
          </li>
        ))}
      </ul>

      <ConfirmarEliminar
        abierto={!!porBorrar}
        onCerrar={() => setPorBorrar(null)}
        onConfirmar={() => porBorrar && eliminar.mutate(porBorrar.id)}
        titulo={`¿Eliminar “${porBorrar?.nombre}”?`}
        descripcion="Desaparece del sitio. Las solicitudes de reserva ya recibidas conservan el nombre del destino. Las imágenes siguen en la biblioteca."
      />
    </>
  );
}
