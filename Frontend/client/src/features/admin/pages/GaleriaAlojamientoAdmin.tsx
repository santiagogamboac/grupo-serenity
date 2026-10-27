import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ImageIcon, Plus } from "lucide-react";
import { api, mensajeDeError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { BotonEliminar, moverId, Ordenar, SelectorImagen } from "../componentes";
import type { AlojamientoImagenAdmin } from "../tipos";

/** Galería y landing se invalidan juntas: la página de detalle lee las fotos de /api/public/home. */
function useMutacionGaleria<TVars>(fn: (v: TVars) => Promise<unknown>, exito?: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "alojamientos"] });
      qc.invalidateQueries({ queryKey: ["public"] });
      if (exito) toast.success(exito);
    },
    onError: e => toast.error(mensajeDeError(e)),
  });
}

/**
 * Fotos adicionales del alojamiento para su página de detalle. La portada sigue siendo la imagen del formulario;
 * estas se muestran después, en este orden.
 */
export function GaleriaAlojamientoAdmin({
  alojamientoId,
  imagenes,
}: {
  alojamientoId: number;
  imagenes: AlojamientoImagenAdmin[];
}) {
  const [selector, setSelector] = useState(false);
  const base = `/admin/alojamientos/${alojamientoId}/imagenes`;
  const agregar = useMutacionGaleria((mediaId: number) => api.post(base, { mediaId, textoAlt: "" }));
  const quitar = useMutacionGaleria((imagenId: number) => api.delete(`${base}/${imagenId}`));
  const reordenar = useMutacionGaleria((ids: number[]) => api.put(`${base}/orden`, { ids }));

  return (
    <section className="mt-6" aria-label="Galería del alojamiento">
      <div className="mb-3 flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">Galería</h3>
          <p className="text-sm text-muted-foreground">
            Fotos extra para la página del alojamiento. La portada es la imagen de arriba; estas van después, en este orden.
          </p>
        </div>
        <Button type="button" variant="outline" onClick={() => setSelector(true)}>
          <Plus /> Agregar foto
        </Button>
      </div>

      {imagenes.length === 0 ? (
        <p className="rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">
          Sin fotos extra: la página muestra la portada (o, si no hay, las fotos del destino).
        </p>
      ) : (
        <ul className="space-y-3">
          {imagenes.map((img, i) => (
            <FilaFoto
              key={img.id}
              base={base}
              img={img}
              indice={i}
              total={imagenes.length}
              ocupado={reordenar.isPending}
              onMover={d => reordenar.mutate(moverId(imagenes, i, d))}
              onQuitar={() => quitar.mutate(img.id)}
            />
          ))}
        </ul>
      )}

      <SelectorImagen abierto={selector} onCerrar={() => setSelector(false)} onElegir={m => agregar.mutate(m.id)} />
    </section>
  );
}

function FilaFoto({
  base,
  img,
  indice,
  total,
  ocupado,
  onMover,
  onQuitar,
}: {
  base: string;
  img: AlojamientoImagenAdmin;
  indice: number;
  total: number;
  ocupado: boolean;
  onMover: (d: -1 | 1) => void;
  onQuitar: () => void;
}) {
  const [alt, setAlt] = useState(img.textoAlt);
  const guardarAlt = useMutacionGaleria(
    (textoAlt: string) => api.put(`${base}/${img.id}`, { textoAlt }),
    "Texto alternativo guardado"
  );
  useEffect(() => setAlt(img.textoAlt), [img.textoAlt]);

  return (
    <li className="flex items-center gap-3 rounded-lg border bg-card p-3">
      <Ordenar indice={indice} total={total} deshabilitado={ocupado} onMover={onMover} />
      <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-md bg-muted">
        {img.url ? <img src={img.url} alt={img.textoAlt} className="size-full object-cover" /> : <ImageIcon />}
      </div>
      <Input
        className="min-w-0 flex-1"
        value={alt}
        maxLength={200}
        placeholder="Texto alternativo (describe la foto)"
        aria-label={`Texto alternativo de la foto ${indice + 1}`}
        onChange={e => setAlt(e.target.value)}
        onBlur={() => alt !== img.textoAlt && guardarAlt.mutate(alt)}
      />
      <BotonEliminar onClick={onQuitar} etiqueta="Quitar de la galería" />
    </li>
  );
}
