import { forwardRef, useRef, useState, type ComponentProps, type ReactNode } from "react";
import { ArrowDown, ArrowUp, ImageIcon, Trash2, Upload, X } from "lucide-react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { useMediaLista, useSubirMedia } from "./api";
import type { MediaDetalle } from "./tipos";

export function EncabezadoPagina({
  titulo,
  descripcion,
  acciones,
}: {
  titulo: string;
  descripcion?: string;
  acciones?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{titulo}</h1>
        {descripcion && <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{descripcion}</p>}
      </div>
      {acciones && <div className="flex items-center gap-2">{acciones}</div>}
    </div>
  );
}

/** Etiqueta + control + ayuda + error, para formularios con react-hook-form. */
export function Campo({
  etiqueta,
  ayuda,
  error,
  htmlFor,
  children,
}: {
  etiqueta: string;
  ayuda?: string;
  error?: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={htmlFor}>{etiqueta}</Label>
      {children}
      {ayuda && !error && <p className="text-xs text-muted-foreground">{ayuda}</p>}
      {error && (
        <p className="text-xs font-medium text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

export function ConfirmarEliminar({
  abierto,
  onCerrar,
  onConfirmar,
  titulo,
  descripcion,
}: {
  abierto: boolean;
  onCerrar: () => void;
  onConfirmar: () => void;
  titulo: string;
  descripcion: string;
}) {
  return (
    <AlertDialog open={abierto} onOpenChange={o => !o && onCerrar()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{titulo}</AlertDialogTitle>
          <AlertDialogDescription>{descripcion}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancelar</AlertDialogCancel>
          <AlertDialogAction
            className="bg-destructive text-white hover:bg-destructive/90"
            onClick={() => {
              onConfirmar();
              onCerrar();
            }}
          >
            Eliminar
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

/** Botones subir/bajar para listas ordenables. */
export function Ordenar({
  indice,
  total,
  onMover,
  deshabilitado,
}: {
  indice: number;
  total: number;
  onMover: (delta: -1 | 1) => void;
  deshabilitado?: boolean;
}) {
  return (
    <div className="flex flex-col">
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label="Subir"
        disabled={deshabilitado || indice === 0}
        onClick={() => onMover(-1)}
      >
        <ArrowUp />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label="Bajar"
        disabled={deshabilitado || indice === total - 1}
        onClick={() => onMover(1)}
      >
        <ArrowDown />
      </Button>
    </div>
  );
}

/** Nuevo orden de ids tras mover el elemento `indice` en `delta` posiciones. */
export function moverId<T extends { id: number }>(lista: T[], indice: number, delta: -1 | 1): number[] {
  const ids = lista.map(x => x.id);
  const destino = indice + delta;
  if (destino < 0 || destino >= ids.length) return ids;
  [ids[indice], ids[destino]] = [ids[destino], ids[indice]];
  return ids;
}

export const formatoBytes = (n: number) =>
  n >= 1024 * 1024 ? `${(n / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`;

export const formatoFecha = (iso: string) =>
  new Date(iso).toLocaleString("es-CO", { dateStyle: "medium", timeStyle: "short", timeZone: "America/Bogota" });

// ─── Selector de imagen (biblioteca + subida) ─────────────────────────────────

export interface MediaRef {
  id: number;
  url: string;
}

export function SelectorImagen({
  abierto,
  onCerrar,
  onElegir,
}: {
  abierto: boolean;
  onCerrar: () => void;
  onElegir: (m: MediaRef) => void;
}) {
  const [pagina, setPagina] = useState(1);
  const { data, isPending } = useMediaLista(pagina, 12);
  const subir = useSubirMedia();
  const input = useRef<HTMLInputElement>(null);

  const alSubir = async (archivos: FileList | null) => {
    const archivo = archivos?.[0];
    if (!archivo) return;
    const m = await subir.mutateAsync(archivo).catch(() => null);
    if (input.current) input.current.value = "";
    if (m) {
      onElegir({ id: m.id, url: m.url });
      onCerrar();
    }
  };

  return (
    <Dialog open={abierto} onOpenChange={o => !o && onCerrar()}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Elegir imagen</DialogTitle>
          <DialogDescription>Selecciona una imagen de la biblioteca o sube una nueva (JPEG, PNG, WebP o GIF, máx. 10 MB).</DialogDescription>
        </DialogHeader>

        <div>
          <input
            ref={input}
            type="file"
            accept="image/jpeg,image/png,image/webp,image/gif"
            className="hidden"
            onChange={e => alSubir(e.target.files)}
            data-testid="selector-subir"
          />
          <Button type="button" variant="outline" disabled={subir.isPending} onClick={() => input.current?.click()}>
            <Upload /> {subir.isPending ? "Subiendo…" : "Subir nueva imagen"}
          </Button>
        </div>

        {isPending ? (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="aspect-square" />
            ))}
          </div>
        ) : (
          <div className="grid max-h-[50vh] grid-cols-3 gap-3 overflow-y-auto sm:grid-cols-4">
            {data?.items.map((m: MediaDetalle) => (
              <button
                key={m.id}
                type="button"
                className="group relative aspect-square overflow-hidden rounded-md border bg-muted focus-visible:ring-2 focus-visible:ring-ring"
                onClick={() => {
                  onElegir({ id: m.id, url: m.url });
                  onCerrar();
                }}
                title={m.nombreOriginal}
              >
                <img src={m.url} alt={m.nombreOriginal} className="size-full object-cover transition group-hover:scale-105" />
              </button>
            ))}
            {data?.items.length === 0 && <p className="col-span-full text-sm text-muted-foreground">Aún no hay imágenes.</p>}
          </div>
        )}

        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between text-sm">
            <Button type="button" variant="outline" size="sm" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>
              Anterior
            </Button>
            <span className="text-muted-foreground">
              Página {data.page} de {data.totalPages}
            </span>
            <Button type="button" variant="outline" size="sm" disabled={pagina >= data.totalPages} onClick={() => setPagina(p => p + 1)}>
              Siguiente
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** Campo de imagen: vista previa + elegir/quitar. */
export function CampoImagen({
  etiqueta,
  ayuda,
  valor,
  onChange,
}: {
  etiqueta: string;
  ayuda?: string;
  valor: MediaRef | null | undefined;
  onChange: (m: MediaRef | null) => void;
}) {
  const [abierto, setAbierto] = useState(false);

  return (
    <Campo etiqueta={etiqueta} ayuda={ayuda}>
      <div className="flex items-center gap-3">
        <div className="flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-md border bg-muted">
          {valor ? <img src={valor.url} alt="" className="size-full object-contain" /> : <ImageIcon className="text-muted-foreground" />}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => setAbierto(true)}>
            {valor ? "Cambiar" : "Elegir imagen"}
          </Button>
          {valor && (
            <Button type="button" variant="ghost" size="sm" onClick={() => onChange(null)}>
              <X /> Quitar
            </Button>
          )}
        </div>
      </div>
      <SelectorImagen abierto={abierto} onCerrar={() => setAbierto(false)} onElegir={onChange} />
    </Campo>
  );
}

export function BotonEliminar({ onClick, etiqueta = "Eliminar" }: { onClick: () => void; etiqueta?: string }) {
  return (
    <Button type="button" variant="ghost" size="icon-sm" aria-label={etiqueta} title={etiqueta} onClick={onClick}>
      <Trash2 className="text-destructive" />
    </Button>
  );
}

/** <select> nativo con el aspecto de <Input>: más simple y accesible que Radix Select para formularios cortos. */
export const SelectNativo = forwardRef<HTMLSelectElement, ComponentProps<"select">>(function SelectNativo(
  { className, ...props },
  ref
) {
  return (
    <select
      ref={ref}
      className={cn(
        "h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:opacity-50",
        className
      )}
      {...props}
    />
  );
});
