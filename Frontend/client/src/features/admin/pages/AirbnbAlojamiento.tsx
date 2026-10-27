import { useState } from "react";
import { toast } from "sonner";
import { Copy, Link2, Plus, Unlink } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useActualizarAlojamiento, useAlojamientos, useAnuncios } from "../api";
import { SelectNativo } from "../componentes";
import type { AlojamientoAdmin, AlojamientoInput } from "../tipos";
import { FilaAnuncio, FormularioAnuncio } from "./AnunciosProyecto";

/** Datos editables del alojamiento con otro anuncio vinculado (el PUT exige el objeto completo). */
const conAnuncio = (a: AlojamientoAdmin, anuncioId: number | null): AlojamientoInput => ({
  nombre: a.nombre,
  descripcion: a.descripcion,
  imagenMediaId: a.imagenMediaId,
  capacidad: a.capacidad,
  huespedesIncluidos: a.huespedesIncluidos,
  precioNoche: a.precioNoche,
  recargoHuespedExtra: a.recargoHuespedExtra,
  tarifaAseo: a.tarifaAseo,
  estanciaMinima: a.estanciaMinima,
  anuncioId,
  publicado: a.publicado,
});

/**
 * Todo lo de Airbnb de un alojamiento en un solo lugar: el anuncio vinculado (su calendario entra al sitio)
 * y el feed propio (el calendario del sitio entra a Airbnb). Así no hay que armar el vínculo en dos pantallas.
 */
export function AirbnbAlojamiento({ alojamiento }: { alojamiento: AlojamientoAdmin }) {
  const { data: anuncios } = useAnuncios(alojamiento.proyectoId);
  const { data: alojamientos } = useAlojamientos(alojamiento.proyectoId);
  const actualizar = useActualizarAlojamiento();
  const [modo, setModo] = useState<"ver" | "nuevo" | "editar">("ver");
  const [elegido, setElegido] = useState("");

  const vinculado = anuncios?.find(a => a.id === alojamiento.anuncioId) ?? null;
  const usados = new Set((alojamientos ?? []).filter(x => x.id !== alojamiento.id).map(x => x.anuncioId));
  const libres = (anuncios ?? []).filter(a => !usados.has(a.id));
  const vincular = (anuncioId: number | null) =>
    actualizar.mutateAsync({ id: alojamiento.id, datos: conAnuncio(alojamiento, anuncioId) });

  return (
    <section className="space-y-4 lg:col-span-2" aria-label="Airbnb">
      <div>
        <h4 className="text-sm font-semibold">Airbnb</h4>
        <p className="text-xs text-muted-foreground">
          Si este alojamiento también se publica en Airbnb, conéctalo en dos pasos: (1) el calendario de Airbnb entra al sitio y
          (2) el calendario del sitio entra a Airbnb. Así ninguno vende noches que el otro ya vendió.
        </p>
      </div>

      <div className="rounded-lg border p-4">
        <h5 className="mb-2 text-sm font-medium">1. Calendario de Airbnb → sitio</h5>
        {modo === "nuevo" ? (
          <FormularioAnuncio
            proyectoId={alojamiento.proyectoId}
            anuncio={null}
            nombreInicial={alojamiento.nombre}
            onGuardado={a => vincular(a.id)}
            onCerrar={() => setModo("ver")}
          />
        ) : modo === "editar" && vinculado ? (
          <FormularioAnuncio proyectoId={alojamiento.proyectoId} anuncio={vinculado} onCerrar={() => setModo("ver")} />
        ) : vinculado ? (
          <div className="space-y-2">
            <ul>
              <FilaAnuncio anuncio={vinculado} onEditar={() => setModo("editar")} />
            </ul>
            <Button type="button" variant="ghost" size="sm" disabled={actualizar.isPending} onClick={() => vincular(null)}>
              <Unlink /> Desvincular de este alojamiento
            </Button>
          </div>
        ) : (
          <div className="flex flex-wrap items-end gap-2">
            <Button type="button" variant="outline" onClick={() => setModo("nuevo")}>
              <Plus /> Conectar anuncio de Airbnb
            </Button>
            {libres.length > 0 && (
              <>
                <SelectNativo
                  aria-label="Anuncio ya registrado"
                  value={elegido}
                  onChange={e => setElegido(e.target.value)}
                  className="w-auto"
                >
                  <option value="">o elige uno ya registrado…</option>
                  {libres.map(a => (
                    <option key={a.id} value={a.id}>
                      {a.nombre} (#{a.airbnbRoomId})
                    </option>
                  ))}
                </SelectNativo>
                <Button
                  type="button"
                  variant="outline"
                  disabled={!elegido || actualizar.isPending}
                  onClick={() => vincular(Number(elegido)).then(() => setElegido(""))}
                >
                  <Link2 /> Vincular
                </Button>
              </>
            )}
          </div>
        )}
      </div>

      <FeedPropio url={alojamiento.urlIcal} />
    </section>
  );
}

function FeedPropio({ url }: { url: string }) {
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(url);
      toast.success("Enlace copiado");
    } catch {
      toast.error("No se pudo copiar: selecciónalo y cópialo a mano.");
    }
  };

  return (
    <div className="rounded-lg border p-4">
      <h5 className="mb-1 text-sm font-medium">2. Calendario del sitio → Airbnb</h5>
      <p className="mb-2 text-xs text-muted-foreground">
        En Airbnb → Calendario → elige el anuncio → Disponibilidad → Sincronizar calendarios → Importar calendario, pega este enlace
        y ponle de nombre «Página web propia». Es privado: no lo compartas.
      </p>
      <div className="flex gap-2">
        <Input
          readOnly
          value={url}
          aria-label="Enlace iCal del alojamiento"
          onFocus={e => e.currentTarget.select()}
          className="font-mono text-xs"
        />
        <Button type="button" variant="outline" onClick={copiar}>
          <Copy /> Copiar
        </Button>
      </div>
    </div>
  );
}
