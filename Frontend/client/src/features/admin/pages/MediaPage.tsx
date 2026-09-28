import { useRef, useState } from "react";
import { Upload } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useEliminarMedia, useMediaLista, useSubirMedia } from "../api";
import { BotonEliminar, ConfirmarEliminar, EncabezadoPagina, formatoBytes } from "../componentes";
import type { MediaDetalle } from "../tipos";

export function MediaPage() {
  const [pagina, setPagina] = useState(1);
  const { data, isPending } = useMediaLista(pagina, 24);
  const subir = useSubirMedia();
  const eliminar = useEliminarMedia();
  const input = useRef<HTMLInputElement>(null);
  const [porBorrar, setPorBorrar] = useState<MediaDetalle | null>(null);

  const alElegir = async (archivos: FileList | null) => {
    if (!archivos) return;
    let subidas = 0;
    for (const archivo of Array.from(archivos)) {
      const ok = await subir.mutateAsync(archivo).then(() => true, () => false);
      if (ok) subidas++;
    }
    if (subidas > 0) toast.success(subidas === 1 ? "Imagen subida" : `${subidas} imágenes subidas`);
    if (input.current) input.current.value = "";
    setPagina(1);
  };

  return (
    <>
      <EncabezadoPagina
        titulo="Imágenes"
        descripcion="Biblioteca de imágenes del sitio. Una imagen en uso (en un proyecto o en una sección) no se puede eliminar hasta quitarla de ahí."
        acciones={
          <>
            <input ref={input} type="file" multiple accept="image/jpeg,image/png,image/webp,image/gif" className="hidden" onChange={e => alElegir(e.target.files)} data-testid="media-subir" />
            <Button onClick={() => input.current?.click()} disabled={subir.isPending}>
              <Upload /> {subir.isPending ? "Subiendo…" : "Subir imágenes"}
            </Button>
          </>
        }
      />

      {isPending && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="aspect-[4/3]" />
          ))}
        </div>
      )}
      {data?.items.length === 0 && <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">La biblioteca está vacía.</p>}

      <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
        {data?.items.map(m => (
          <li key={m.id} className="overflow-hidden rounded-lg border bg-card" data-testid={`media-${m.id}`}>
            <a href={m.url} target="_blank" rel="noreferrer" className="block aspect-[4/3] bg-muted">
              <img src={m.url} alt={m.nombreOriginal} loading="lazy" className="size-full object-cover" />
            </a>
            <div className="flex items-center justify-between gap-2 p-2">
              <div className="min-w-0">
                <p className="truncate text-xs font-medium" title={m.nombreOriginal}>
                  {m.nombreOriginal}
                </p>
                <p className="text-[11px] text-muted-foreground">{formatoBytes(m.tamanoBytes)}</p>
              </div>
              <BotonEliminar onClick={() => setPorBorrar(m)} />
            </div>
          </li>
        ))}
      </ul>

      {data && data.totalPages > 1 && (
        <div className="mt-6 flex items-center justify-between text-sm">
          <Button variant="outline" size="sm" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>
            Anterior
          </Button>
          <span className="text-muted-foreground">
            Página {data.page} de {data.totalPages}
          </span>
          <Button variant="outline" size="sm" disabled={pagina >= data.totalPages} onClick={() => setPagina(p => p + 1)}>
            Siguiente
          </Button>
        </div>
      )}

      <ConfirmarEliminar
        abierto={!!porBorrar}
        onCerrar={() => setPorBorrar(null)}
        onConfirmar={() => porBorrar && eliminar.mutate(porBorrar.id)}
        titulo={`¿Eliminar “${porBorrar?.nombreOriginal}”?`}
        descripcion="Se borra del almacenamiento. Si está en uso, el sistema te avisará y no se eliminará."
      />
    </>
  );
}
