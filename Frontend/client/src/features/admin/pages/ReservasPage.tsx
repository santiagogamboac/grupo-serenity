import { useEffect, useState } from "react";
import { Copy, ExternalLink, Search } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { formatoDia, formatoPesos } from "@/lib/reservas";
import {
  useCancelarReserva,
  useCatalogos,
  useConfirmarReserva,
  useNotasReserva,
  useReservas,
} from "../api";
import {
  Campo,
  EncabezadoPagina,
  formatoFecha,
  SelectNativo,
} from "../componentes";
import type { ReservaAdmin } from "../tipos";

export const ETIQUETA_ESTADO_RESERVA: Record<string, string> = {
  pendiente_pago: "Pendiente de pago",
  confirmada: "Confirmada",
  cancelada: "Cancelada",
  expirada: "Expirada",
  revision: "En revisión",
};

const CLASE_ESTADO: Record<string, string> = {
  pendiente_pago: "bg-amber-50 text-amber-800 border-amber-200",
  confirmada: "bg-emerald-100 text-emerald-800 border-emerald-200",
  cancelada: "bg-slate-100 text-slate-600 border-slate-200",
  expirada: "bg-slate-100 text-slate-500 border-slate-200",
  revision: "bg-rose-50 text-rose-700 border-rose-200",
};

const ETIQUETA_PAGO: Record<string, string> = {
  APPROVED: "Aprobado",
  PENDING: "En proceso",
  DECLINED: "Rechazado",
  VOIDED: "Anulado",
  ERROR: "Error",
};

const dia = (iso: string) =>
  formatoDia(iso, { day: "numeric", month: "short", year: "numeric" });

export function ReservasPage() {
  const { data: catalogos } = useCatalogos();
  const [estado, setEstado] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const [q, setQ] = useState("");
  const [pagina, setPagina] = useState(1);
  const [abierta, setAbierta] = useState<ReservaAdmin | null>(null);

  useEffect(() => {
    const t = setTimeout(() => {
      setQ(busqueda.trim());
      setPagina(1);
    }, 350);
    return () => clearTimeout(t);
  }, [busqueda]);

  const { data, isPending, isFetching } = useReservas({
    estado,
    q,
    page: pagina,
  });

  return (
    <>
      <EncabezadoPagina
        titulo="Reservas"
        descripcion="Reservas hechas en el sitio. Las pendientes retienen las fechas hasta que vence el plazo de pago."
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="relative min-w-52 flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Buscar por código, huésped, correo, teléfono o alojamiento"
            aria-label="Buscar reservas"
            value={busqueda}
            onChange={e => setBusqueda(e.target.value)}
          />
        </div>
        <SelectNativo
          className="w-48"
          aria-label="Filtrar por estado"
          value={estado}
          onChange={e => {
            setEstado(e.target.value);
            setPagina(1);
          }}
        >
          <option value="">Todos los estados</option>
          {catalogos?.estadosReserva.map(s => (
            <option key={s} value={s}>
              {ETIQUETA_ESTADO_RESERVA[s] ?? s}
            </option>
          ))}
        </SelectNativo>
      </div>

      {isPending ? (
        <Skeleton className="h-64" />
      ) : (
        <div
          className={`overflow-x-auto rounded-lg border bg-card transition-opacity ${isFetching ? "opacity-70" : ""}`}
        >
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Código</TableHead>
                <TableHead>Huésped</TableHead>
                <TableHead>Alojamiento</TableHead>
                <TableHead>Fechas</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead>Estado</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data?.items.map(r => (
                <TableRow
                  key={r.id}
                  className="cursor-pointer"
                  onClick={() => setAbierta(r)}
                  data-testid={`reserva-${r.codigo}`}
                >
                  <TableCell className="whitespace-nowrap font-mono text-xs">
                    {r.codigo}
                  </TableCell>
                  <TableCell className="font-medium">{r.nombre}</TableCell>
                  <TableCell>
                    {r.alojamientoNombre}
                    <div className="text-xs text-muted-foreground">
                      {r.proyectoNombre}
                    </div>
                  </TableCell>
                  <TableCell className="whitespace-nowrap">
                    {dia(r.llegada)} → {dia(r.salida)}
                    <div className="text-xs text-muted-foreground">
                      {r.noches} {r.noches === 1 ? "noche" : "noches"} ·{" "}
                      {r.huespedes} huésp.
                    </div>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {formatoPesos(r.total)}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline" className={CLASE_ESTADO[r.estado]}>
                      {ETIQUETA_ESTADO_RESERVA[r.estado] ?? r.estado}
                    </Badge>
                  </TableCell>
                </TableRow>
              ))}
              {data?.items.length === 0 && (
                <TableRow>
                  <TableCell
                    colSpan={6}
                    className="py-10 text-center text-muted-foreground"
                  >
                    No hay reservas con esos filtros.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </div>
      )}

      {data && data.totalPages > 1 && (
        <div className="mt-4 flex items-center justify-between text-sm">
          <Button
            variant="outline"
            size="sm"
            disabled={pagina <= 1}
            onClick={() => setPagina(p => p - 1)}
          >
            Anterior
          </Button>
          <span className="text-muted-foreground">
            Página {data.page} de {data.totalPages} · {data.totalCount} reservas
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={pagina >= data.totalPages}
            onClick={() => setPagina(p => p + 1)}
          >
            Siguiente
          </Button>
        </div>
      )}

      {abierta && (
        <DetalleReserva
          key={abierta.id}
          reserva={abierta}
          onCambio={setAbierta}
          onCerrar={() => setAbierta(null)}
        />
      )}
    </>
  );
}

function DetalleReserva({
  reserva: r,
  onCambio,
  onCerrar,
}: {
  reserva: ReservaAdmin;
  onCambio: (r: ReservaAdmin) => void;
  onCerrar: () => void;
}) {
  const confirmar = useConfirmarReserva();
  const cancelar = useCancelarReserva();
  const guardarNotas = useNotasReserva();
  const [notas, setNotas] = useState(r.notas ?? "");
  const [cancelando, setCancelando] = useState(false);
  const [motivo, setMotivo] = useState("");
  const [avisar, setAvisar] = useState(true);

  const activa = r.estado !== "cancelada" && r.estado !== "expirada";
  const puedeConfirmar = r.estado !== "confirmada";

  const copiarEnlace = async () => {
    try {
      await navigator.clipboard.writeText(r.enlaceHuesped);
      toast.success(
        "Enlace copiado: envíaselo al huésped para que vea su reserva o pague."
      );
    } catch {
      toast.error("No se pudo copiar el enlace.");
    }
  };

  return (
    <Dialog open onOpenChange={o => !o && onCerrar()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle className="flex flex-wrap items-center gap-2">
            <span className="font-mono">{r.codigo}</span>
            <Badge variant="outline" className={CLASE_ESTADO[r.estado]}>
              {ETIQUETA_ESTADO_RESERVA[r.estado] ?? r.estado}
            </Badge>
          </DialogTitle>
          <DialogDescription>
            Creada el {formatoFecha(r.creadoEn)}
          </DialogDescription>
        </DialogHeader>

        {r.motivoEstado && (
          <p
            className={`rounded-md px-3 py-2 text-sm ${r.estado === "revision" ? "bg-rose-50 text-rose-800" : "bg-muted"}`}
            role="status"
          >
            {r.motivoEstado}
          </p>
        )}

        <dl className="grid grid-cols-[130px_1fr] gap-x-3 gap-y-2 text-sm">
          <dt className="text-muted-foreground">Huésped</dt>
          <dd className="font-medium">{r.nombre}</dd>
          <dt className="text-muted-foreground">Contacto</dt>
          <dd>
            <a
              className="underline-offset-2 hover:underline"
              href={`mailto:${r.correo}`}
            >
              {r.correo}
            </a>{" "}
            ·{" "}
            <a
              className="underline-offset-2 hover:underline"
              href={`https://wa.me/${r.telefono.replace(/\D/g, "")}`}
              target="_blank"
              rel="noreferrer"
            >
              {r.telefono}
            </a>
          </dd>
          <dt className="text-muted-foreground">Alojamiento</dt>
          <dd>
            {r.alojamientoNombre} · {r.proyectoNombre}
          </dd>
          <dt className="text-muted-foreground">Fechas</dt>
          <dd>
            {dia(r.llegada)} → {dia(r.salida)} ({r.noches}{" "}
            {r.noches === 1 ? "noche" : "noches"})
          </dd>
          <dt className="text-muted-foreground">Huéspedes</dt>
          <dd>{r.huespedes}</dd>
          {r.notaHuesped && (
            <>
              <dt className="text-muted-foreground">Nota del huésped</dt>
              <dd className="whitespace-pre-line">{r.notaHuesped}</dd>
            </>
          )}
          <dt className="text-muted-foreground">Importe</dt>
          <dd className="tabular-nums">
            {formatoPesos(r.subtotal)} + aseo {formatoPesos(r.tarifaAseo)} ={" "}
            <strong>{formatoPesos(r.total)}</strong>
          </dd>
          <dt className="text-muted-foreground">Pago</dt>
          <dd>
            {r.pagoProveedor === "manual"
              ? "Registrado a mano"
              : r.pagoEstado
                ? `Wompi · ${ETIQUETA_PAGO[r.pagoEstado] ?? r.pagoEstado}${r.pagoMetodo ? ` · ${r.pagoMetodo}` : ""}`
                : "Sin pago todavía"}
            {r.pagoTransaccionId && (
              <div className="font-mono text-xs text-muted-foreground">
                Transacción {r.pagoTransaccionId}
              </div>
            )}
            {r.pagadoEn && (
              <div className="text-xs text-muted-foreground">
                Pagada el {formatoFecha(r.pagadoEn)}
              </div>
            )}
          </dd>
          {r.expiraEn && (
            <>
              <dt className="text-muted-foreground">Retención hasta</dt>
              <dd>{formatoFecha(r.expiraEn)}</dd>
            </>
          )}
        </dl>

        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={copiarEnlace}
          >
            <Copy /> Copiar enlace del huésped
          </Button>
          <Button type="button" variant="ghost" size="sm" asChild>
            <a href={r.enlaceHuesped} target="_blank" rel="noreferrer">
              <ExternalLink /> Ver como huésped
            </a>
          </Button>
        </div>

        <Campo etiqueta="Notas internas" htmlFor="notas-reserva">
          <Textarea
            id="notas-reserva"
            rows={3}
            maxLength={4000}
            value={notas}
            onChange={e => setNotas(e.target.value)}
          />
        </Campo>

        {cancelando && (
          <div className="space-y-3 rounded-md border border-destructive/30 bg-destructive/5 p-3">
            <Campo
              etiqueta="Motivo de la cancelación"
              htmlFor="motivo-cancelar"
              ayuda="Lo verá el huésped en su reserva."
            >
              <Input
                id="motivo-cancelar"
                maxLength={300}
                value={motivo}
                onChange={e => setMotivo(e.target.value)}
              />
            </Campo>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={avisar}
                onCheckedChange={v => setAvisar(v === true)}
              />{" "}
              Avisar al huésped por correo
            </label>
            <p className="text-xs text-muted-foreground">
              Las fechas se liberan al instante. Si ya pagó con Wompi, el
              reembolso se hace desde el dashboard de Wompi.
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="destructive"
                disabled={!motivo.trim() || cancelar.isPending}
                onClick={() =>
                  cancelar.mutate(
                    { id: r.id, motivo: motivo.trim(), avisarHuesped: avisar },
                    {
                      onSuccess: nueva => (
                        onCambio(nueva),
                        setCancelando(false)
                      ),
                    }
                  )
                }
              >
                {cancelar.isPending ? "Cancelando…" : "Confirmar cancelación"}
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => setCancelando(false)}
              >
                Volver
              </Button>
            </div>
          </div>
        )}

        <DialogFooter className="gap-2 sm:justify-between">
          <div className="flex gap-2">
            {activa && !cancelando && (
              <Button
                type="button"
                variant="ghost"
                className="text-destructive"
                onClick={() => setCancelando(true)}
              >
                Cancelar reserva
              </Button>
            )}
          </div>
          <div className="flex flex-wrap gap-2">
            {puedeConfirmar && (
              <Button
                type="button"
                variant="outline"
                disabled={confirmar.isPending}
                title="El pago llegó por otro medio, o la revisión ya se resolvió"
                onClick={() => confirmar.mutate(r.id, { onSuccess: onCambio })}
              >
                {confirmar.isPending ? "Confirmando…" : "Confirmar reserva"}
              </Button>
            )}
            <Button
              type="button"
              disabled={guardarNotas.isPending || notas === (r.notas ?? "")}
              onClick={() =>
                guardarNotas.mutate(
                  { id: r.id, notas },
                  { onSuccess: onCambio }
                )
              }
            >
              {guardarNotas.isPending ? "Guardando…" : "Guardar notas"}
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
