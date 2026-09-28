import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Pencil, Plus, Star } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useActualizarResena, useCrearResena, useEliminarResena, useReordenarResenas, useResenas } from "../api";
import { BotonEliminar, Campo, ConfirmarEliminar, EncabezadoPagina, moverId, Ordenar, SelectNativo } from "../componentes";
import type { ResenaAdmin } from "../tipos";

const esquema = z.object({
  autor: z.string().trim().min(1, "Requerido.").max(120),
  contexto: z.string().trim().max(120),
  texto: z.string().trim().min(1, "Requerido.").max(600),
  calificacion: z.string(),
  publicada: z.boolean(),
});
type Valores = z.infer<typeof esquema>;

export function ResenasPage() {
  const { data: resenas, isPending } = useResenas();
  const reordenar = useReordenarResenas();
  const eliminar = useEliminarResena();
  const [editando, setEditando] = useState<ResenaAdmin | "nueva" | null>(null);
  const [porBorrar, setPorBorrar] = useState<ResenaAdmin | null>(null);

  return (
    <>
      <EncabezadoPagina
        titulo="Reseñas"
        descripcion="Testimonios de la sección “Palabras que nos quedan”. Solo se muestran las publicadas."
        acciones={
          <Button onClick={() => setEditando("nueva")}>
            <Plus /> Nueva reseña
          </Button>
        }
      />

      {isPending && <Skeleton className="h-32" />}
      {resenas?.length === 0 && <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">Aún no hay reseñas.</p>}

      <ul className="space-y-3">
        {resenas?.map((r, i) => (
          <li key={r.id} className="flex items-start gap-3 rounded-lg border bg-card p-3" data-testid={`resena-${r.id}`}>
            <Ordenar indice={i} total={resenas.length} deshabilitado={reordenar.isPending} onMover={d => reordenar.mutate(moverId(resenas, i, d))} />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{r.autor}</span>
                <span className="flex text-emerald-700">
                  {Array.from({ length: r.calificacion }).map((_, n) => (
                    <Star key={n} className="size-3.5" fill="currentColor" />
                  ))}
                </span>
                {!r.publicada && <Badge variant="outline">Oculta</Badge>}
              </div>
              <p className="text-xs text-muted-foreground">{r.contexto}</p>
              <p className="mt-1 text-sm">“{r.texto}”</p>
            </div>
            <Button variant="outline" size="icon-sm" aria-label={`Editar reseña de ${r.autor}`} onClick={() => setEditando(r)}>
              <Pencil />
            </Button>
            <BotonEliminar onClick={() => setPorBorrar(r)} />
          </li>
        ))}
      </ul>

      {editando && <FormularioResena key={editando === "nueva" ? "nueva" : editando.id} resena={editando === "nueva" ? null : editando} onCerrar={() => setEditando(null)} />}

      <ConfirmarEliminar
        abierto={!!porBorrar}
        onCerrar={() => setPorBorrar(null)}
        onConfirmar={() => porBorrar && eliminar.mutate(porBorrar.id)}
        titulo={`¿Eliminar la reseña de ${porBorrar?.autor}?`}
        descripcion="Esta acción no se puede deshacer. Si solo quieres ocultarla, desmarca “Publicada”."
      />
    </>
  );
}

function FormularioResena({ resena, onCerrar }: { resena: ResenaAdmin | null; onCerrar: () => void }) {
  const crear = useCrearResena();
  const actualizar = useActualizarResena();
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({
    resolver: zodResolver(esquema),
    defaultValues: resena
      ? { ...resena, calificacion: String(resena.calificacion) }
      : { autor: "", contexto: "", texto: "", calificacion: "5", publicada: true },
  });

  const guardar = handleSubmit(async v => {
    const datos = { ...v, calificacion: Number(v.calificacion) };
    try {
      if (resena) await actualizar.mutateAsync({ id: resena.id, datos });
      else await crear.mutateAsync(datos);
      onCerrar();
    } catch {
      /* toast de error ya mostrado */
    }
  });

  return (
    <Dialog open onOpenChange={o => !o && onCerrar()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{resena ? "Editar reseña" : "Nueva reseña"}</DialogTitle>
          <DialogDescription>Aparece en la sección “Palabras que nos quedan” cuando está publicada.</DialogDescription>
        </DialogHeader>
        <form onSubmit={guardar} noValidate className="space-y-4">
          <Campo etiqueta="Autor" htmlFor="autor" error={errors.autor?.message}>
            <Input id="autor" {...register("autor")} />
          </Campo>
          <Campo etiqueta="Contexto" htmlFor="contexto" error={errors.contexto?.message} ayuda="Ej.: Estadía en Santa Marta">
            <Input id="contexto" {...register("contexto")} />
          </Campo>
          <Campo etiqueta="Comentario" htmlFor="texto" error={errors.texto?.message}>
            <Textarea id="texto" rows={4} {...register("texto")} />
          </Campo>
          <Campo etiqueta="Calificación" htmlFor="calificacion">
            <SelectNativo id="calificacion" {...register("calificacion")}>
              {[5, 4, 3, 2, 1].map(n => (
                <option key={n} value={n}>
                  {n} {n === 1 ? "estrella" : "estrellas"}
                </option>
              ))}
            </SelectNativo>
          </Campo>
          <label className="flex items-center gap-3 text-sm">
            <Switch checked={watch("publicada")} onCheckedChange={v => setValue("publicada", v)} aria-label="Publicada" />
            Publicada
          </label>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cancelar
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? "Guardando…" : "Guardar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
