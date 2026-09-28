import { useEffect, useState } from "react";
import { Search } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { useActualizarSolicitud, useCatalogos, useEliminarSolicitud, useSolicitudes } from "../api";
import { Campo, ConfirmarEliminar, EncabezadoPagina, formatoFecha, SelectNativo } from "../componentes";
import type { SolicitudAdmin } from "../tipos";

const ETIQUETA_ESTADO: Record<string, string> = {
  nueva: "Nueva",
  contactada: "Contactada",
  cerrada: "Cerrada",
  descartada: "Descartada",
};

const CLASE_ESTADO: Record<string, string> = {
  nueva: "bg-emerald-100 text-emerald-800 border-emerald-200",
  contactada: "bg-sky-100 text-sky-800 border-sky-200",
  cerrada: "bg-slate-100 text-slate-700 border-slate-200",
  descartada: "bg-rose-50 text-rose-700 border-rose-200",
};

const formatoDia = (yyyyMmDd: string) =>
  new Date(`${yyyyMmDd}T12:00:00`).toLocaleDateString("es-CO", { day: "numeric", month: "short", year: "numeric" });

export function SolicitudesPage() {
  const { data: catalogos } = useCatalogos();
  const [estado, setEstado] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const [q, setQ] = useState("");
  const [pagina, setPagina] = useState(1);
  const [abierta, setAbierta] = useState<SolicitudAdmin | null>(null);

  // La búsqueda se envía 350 ms después de dejar de escribir.
  useEffect(() => {
    const t = setTimeout(() => {
      setQ(busqueda.trim());
      setPagina(1);
    }, 350);
    return () => clearTimeout(t);
  }, [busqueda]);

  const { data, isPending, isFetching } = useSolicitudes({ estado, q, page: pagina });

  return (
    <>
      <EncabezadoPagina titulo="Solicitudes de reserva" descripcion="Lo que llega desde el formulario del sitio. Marca cada una a medida que das seguimiento." />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative min-w-52 flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input className="pl-9" placeholder="Buscar por nombre, contacto o destino" aria-label="Buscar solicitudes" value={busqueda} onChange={e => setBusqueda(e.target.value)} />
        </div>
        <SelectNativo
          className="w-44"
          aria-label="Filtrar por estado"
          value={estado}
          onChange={e => {
            setEstado(e.target.value);
            setPagina(1);
          }}
        >
          <option value="">Todos los estados</option>
          {catalogos?.estadosSolicitud.map(s => (
            <option key={s} value={s}>
              {ETIQUETA_ESTADO[s] ?? s}
            </option>
          ))}
        </SelectNativo>
      </div>

      {isPending ? (
        <Skeleton className="h-64" />
      ) : (
        <div className={`overflow-x-auto rounded-lg border bg-card transition-opacity ${isFetching ? "opacity-70" : ""}`}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Recibida</TableHead>
                <TableHead>Nombre</TableHead>
                <TableHead>Contacto</TableHead>
                <TableHead>Destino</TableHead>
                <TableHead>Llegada</TableHead>
                <TableHead className="text-right">Huésp.</TableHead>
                <TableHead>Estado</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.items.map(s => (
                <TableRow key={s.id} className="cursor-pointer" onClick={() => setAbierta(s)} data-testid={`solicitud-${s.id}`}>
                  <TableCell className="whitespace-nowrap text-muted-foreground">{formatoFecha(s.creadoEn)}</TableCell>
                  <TableCell className="font-medium">{s.nombre}</TableCell>
                  <TableCell>{s.contacto}</TableCell>
                  <TableCell>{s.destinoTexto}</TableCell>
                  <TableCell className="whitespace-nowrap">{formatoDia(s.fechaLlegada)}</TableCell>
                  <TableCell className="text-right tabular-nums">{s.huespedes}</TableCell>
                  <TableCell>
                    <Badge variant="outline" className={CLASE_ESTADO[s.estado]}>
                      {ETIQUETA_ESTADO[s.estado] ?? s.estado}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
              {data?.items.length === 0 && (
                <TableRow>
                  <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">
                    No hay solicitudes con esos filtros.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {data && data.totalPages > 1 && (
        <div className="mt-4 flex items-center justify-between text-sm">
          <Button variant="outline" size="sm" disabled={pagina <= 1} onClick={() => setPagina(p => p - 1)}>
            Anterior
          </Button>
          <span className="text-muted-foreground">
            Página {data.page} de {data.totalPages} · {data.totalCount} solicitudes
          </span>
          <Button variant="outline" size="sm" disabled={pagina >= data.totalPages} onClick={() => setPagina(p => p + 1)}>
            Siguiente
          </Button>
        </div>
      )}

      {abierta && <DetalleSolicitud key={abierta.id} solicitud={abierta} estados={catalogos?.estadosSolicitud ?? []} onCerrar={() => setAbierta(null)} />}
    </>
  );
}

function DetalleSolicitud({ solicitud, estados, onCerrar }: { solicitud: SolicitudAdmin; estados: string[]; onCerrar: () => void }) {
  const actualizar = useActualizarSolicitud();
  const eliminar = useEliminarSolicitud();
  const [estado, setEstado] = useState(solicitud.estado);
  const [notas, setNotas] = useState(solicitud.notas ?? "");
  const [confirmar, setConfirmar] = useState(false);

  return (
    <Dialog open onOpenChange={o => !o && onCerrar()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{solicitud.nombre}</DialogTitle>
          <DialogDescription>Recibida el {formatoFecha(solicitud.creadoEn)}</DialogDescription>
        </DialogHeader>

        <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-2 text-sm">
          <dt className="text-muted-foreground">Contacto</dt>
          <dd className="font-medium">{solicitud.contacto}</dd>
          <dt className="text-muted-foreground">Destino</dt>
          <dd>{solicitud.destinoTexto}</dd>
          <dt className="text-muted-foreground">Llegada</dt>
          <dd>{formatoDia(solicitud.fechaLlegada)}</dd>
          <dt className="text-muted-foreground">Huéspedes</dt>
          <dd>{solicitud.huespedes}</dd>
        </dl>

        <Campo etiqueta="Estado" htmlFor="estado-solicitud">
          <SelectNativo id="estado-solicitud" value={estado} onChange={e => setEstado(e.target.value)}>
            {estados.map(s => (
              <option key={s} value={s}>
                {ETIQUETA_ESTADO[s] ?? s}
              </option>
            ))}
          </SelectNativo>
        </Campo>
        <Campo etiqueta="Notas internas" htmlFor="notas-solicitud">
          <Textarea id="notas-solicitud" rows={4} maxLength={2000} value={notas} onChange={e => setNotas(e.target.value)} placeholder="Ej.: Llamé el lunes, quedó de confirmar fechas." />
        </Campo>

        <DialogFooter className="sm:justify-between">
          <Button type="button" variant="ghost" className="text-destructive" onClick={() => setConfirmar(true)}>
            Eliminar
          </Button>
          <div className="flex gap-2">
            <Button type="button" variant="outline" onClick={onCerrar}>
              Cerrar
            </Button>
            <Button type="button" disabled={actualizar.isPending} onClick={() => actualizar.mutate({ id: solicitud.id, estado, notas }, { onSuccess: onCerrar })}>
              {actualizar.isPending ? "Guardando…" : "Guardar"}
            </Button>
          </div>
        </DialogFooter>

        <ConfirmarEliminar
          abierto={confirmar}
          onCerrar={() => setConfirmar(false)}
          onConfirmar={() => eliminar.mutate(solicitud.id, { onSuccess: onCerrar })}
          titulo="¿Eliminar esta solicitud?"
          descripcion="Se borra definitivamente. Si solo quieres archivarla, cámbiale el estado a “Descartada”."
        />
      </DialogContent>
    </Dialog>
  );
}
