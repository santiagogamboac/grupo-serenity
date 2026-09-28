import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Pencil, Plus } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { iconoDe } from "@/lib/icons";
import { useActualizarValor, useCatalogos, useCrearValor, useEliminarValor, useReordenarValores, useValores } from "../api";
import { BotonEliminar, Campo, ConfirmarEliminar, EncabezadoPagina, moverId, Ordenar, SelectNativo } from "../componentes";
import type { ValorAdmin } from "../tipos";

const esquema = z.object({
  titulo: z.string().trim().min(1, "Requerido.").max(80),
  descripcion: z.string().trim().min(1, "Requerido.").max(300),
  icono: z.string().min(1, "Elige un icono."),
  variante: z.string().min(1),
  activo: z.boolean(),
});
type Valores = z.infer<typeof esquema>;

const DESCRIPCION_VARIANTE: Record<string, string> = {
  normal: "Tarjeta estándar",
  alta: "Tarjeta alta, con línea decorativa",
  ancha: "Tarjeta ancha (ocupa toda la fila)",
};

export function ValoresPage() {
  const { data: valores, isPending } = useValores();
  const reordenar = useReordenarValores();
  const eliminar = useEliminarValor();
  const [editando, setEditando] = useState<ValorAdmin | "nuevo" | null>(null);
  const [porBorrar, setPorBorrar] = useState<ValorAdmin | null>(null);

  return (
    <>
      <EncabezadoPagina
        titulo="Valores"
        descripcion="Las tarjetas de la sección “Lo que nos mueve”. El título y el texto de la sección se editan en Contenido del sitio."
        acciones={
          <Button onClick={() => setEditando("nuevo")}>
            <Plus /> Nuevo valor
          </Button>
        }
      />

      {isPending && <Skeleton className="h-32" />}
      {valores?.length === 0 && <p className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">Aún no hay valores.</p>}

      <ul className="space-y-3">
        {valores?.map((v, i) => {
          const Icono = iconoDe(v.icono);
          return (
            <li key={v.id} className="flex items-center gap-3 rounded-lg border bg-card p-3" data-testid={`valor-${v.id}`}>
              <Ordenar indice={i} total={valores.length} deshabilitado={reordenar.isPending} onMover={d => reordenar.mutate(moverId(valores, i, d))} />
              <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted">
                <Icono className="size-5" />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium">{v.titulo}</span>
                  <Badge variant="secondary">{v.variante}</Badge>
                  {!v.activo && <Badge variant="outline">Oculto</Badge>}
                </div>
                <p className="truncate text-sm text-muted-foreground">{v.descripcion}</p>
              </div>
              <Button variant="outline" size="icon-sm" aria-label={`Editar ${v.titulo}`} onClick={() => setEditando(v)}>
                <Pencil />
              </Button>
              <BotonEliminar onClick={() => setPorBorrar(v)} />
            </li>
          );
        })}
      </ul>

      {editando && <FormularioValor key={editando === "nuevo" ? "nuevo" : editando.id} valor={editando === "nuevo" ? null : editando} onCerrar={() => setEditando(null)} />}

      <ConfirmarEliminar
        abierto={!!porBorrar}
        onCerrar={() => setPorBorrar(null)}
        onConfirmar={() => porBorrar && eliminar.mutate(porBorrar.id)}
        titulo={`¿Eliminar “${porBorrar?.titulo}”?`}
        descripcion="Esta acción no se puede deshacer. Si solo quieres ocultarlo, desmarca “Activo”."
      />
    </>
  );
}

function FormularioValor({ valor, onCerrar }: { valor: ValorAdmin | null; onCerrar: () => void }) {
  const { data: catalogos } = useCatalogos();
  const crear = useCrearValor();
  const actualizar = useActualizarValor();
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Valores>({
    resolver: zodResolver(esquema),
    defaultValues: valor ?? { titulo: "", descripcion: "", icono: "sparkles", variante: "normal", activo: true },
  });

  const guardar = handleSubmit(async datos => {
    try {
      if (valor) await actualizar.mutateAsync({ id: valor.id, datos });
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
          <DialogTitle>{valor ? "Editar valor" : "Nuevo valor"}</DialogTitle>
          <DialogDescription>Tarjeta de la sección “Lo que nos mueve”.</DialogDescription>
        </DialogHeader>
        <form onSubmit={guardar} noValidate className="space-y-4">
          <Campo etiqueta="Título" htmlFor="titulo" error={errors.titulo?.message}>
            <Input id="titulo" {...register("titulo")} />
          </Campo>
          <Campo etiqueta="Descripción" htmlFor="descripcion" error={errors.descripcion?.message}>
            <Textarea id="descripcion" rows={3} {...register("descripcion")} />
          </Campo>
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo etiqueta="Icono" htmlFor="icono" error={errors.icono?.message}>
              <SelectNativo id="icono" {...register("icono")}>
                {(catalogos?.iconos ?? ["sparkles"]).map(i => (
                  <option key={i} value={i}>
                    {i}
                  </option>
                ))}
              </SelectNativo>
            </Campo>
            <Campo etiqueta="Forma de la tarjeta" htmlFor="variante">
              <SelectNativo id="variante" {...register("variante")}>
                {(catalogos?.variantesValor ?? ["normal"]).map(v => (
                  <option key={v} value={v}>
                    {DESCRIPCION_VARIANTE[v] ?? v}
                  </option>
                ))}
              </SelectNativo>
            </Campo>
          </div>
          <label className="flex items-center gap-3 text-sm">
            <Switch checked={watch("activo")} onCheckedChange={v => setValue("activo", v)} aria-label="Activo" />
            Activo (visible en el sitio)
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
