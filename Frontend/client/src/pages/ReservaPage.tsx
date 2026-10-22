import { useEffect, useRef } from "react";
import { Link, useParams, useSearch } from "wouter";
import { toast } from "sonner";
import { AlertTriangle, CheckCircle2, Clock3, XCircle } from "lucide-react";
import { mensajeDeError } from "@/lib/api";
import { formatoDia, formatoPesos } from "@/lib/reservas";
import type { ReservaPublica } from "@/lib/types";
import { TEXTOS } from "@/features/public/textos";
import {
  useHome,
  useNuevoPago,
  useReserva,
  useVerificarPago,
} from "@/features/public/useHome";

const T = TEXTOS.estadoReserva;

type Vista = keyof typeof T.estados;

function vistaDe(r: ReservaPublica): Vista {
  if (r.estado !== "pendiente_pago") return r.estado;
  if (r.pagoEstado === "PENDING") return "pago_en_proceso";
  if (r.pagoEstado && r.pagoEstado !== "APPROVED") return "pago_rechazado";
  return "pendiente_pago";
}

const ICONOS: Record<Vista, typeof CheckCircle2> = {
  confirmada: CheckCircle2,
  pendiente_pago: Clock3,
  pago_en_proceso: Clock3,
  pago_rechazado: XCircle,
  expirada: XCircle,
  cancelada: XCircle,
  revision: AlertTriangle,
};

/** Estado de una reserva: a esta página vuelve el huésped desde Wompi (con ?id=<transacción>). */
export default function ReservaPage() {
  const { codigo = "" } = useParams<{ codigo: string }>();
  const params = new URLSearchParams(useSearch());
  const token = params.get("t");
  const transaccion = params.get("id");

  const { data: home } = useHome();
  const reserva = useReserva(codigo, token);
  const verificar = useVerificarPago(codigo, token);
  const pagar = useNuevoPago(codigo, token);

  // Al volver de Wompi se confirma el resultado en el servidor (una sola vez por transacción).
  const verificada = useRef<string | null>(null);
  useEffect(() => {
    if (transaccion && token && verificada.current !== transaccion) {
      verificada.current = transaccion;
      verificar.mutate(transaccion);
    }
  }, [transaccion, token, verificar]);

  useEffect(() => {
    document.title = `${T.titulo} ${codigo}`;
  }, [codigo]);

  const irAPagar = () =>
    pagar.mutate(undefined, {
      onSuccess: r => window.location.assign(r.checkoutUrl),
      onError: e => toast.error(mensajeDeError(e)),
    });

  const r = reserva.data;
  const vista = r ? vistaDe(r) : null;
  const marca = home?.contenido.marca;

  return (
    <main className="site-shell reserva-page">
      <header className="reserva-page__cabecera">
        <Link
          href="/"
          className="reserva-page__marca"
          aria-label={TEXTOS.inicioAria(marca?.nombre ?? "")}
        >
          {marca?.logo ? (
            <img src={marca.logo.url} alt={marca.logoAlt || marca.nombre} />
          ) : (
            <span>{marca?.nombre}</span>
          )}
        </Link>
      </header>

      <section className="reserva-card" aria-live="polite">
        {!token || reserva.isError ? (
          <div className="reserva-card__estado reserva-card__estado--cancelada">
            <XCircle size={40} aria-hidden="true" />
            <h1>{T.noEncontrada}</h1>
            <p>{T.noEncontradaDetalle}</p>
          </div>
        ) : !r || !vista ? (
          <div className="reserva-card__estado">
            <div className="reserva-spinner" />
            <p>{verificar.isPending ? T.verificando : T.cargando}</p>
          </div>
        ) : (
          <>
            <div
              className={`reserva-card__estado reserva-card__estado--${vista}`}
            >
              {(() => {
                const Icono = ICONOS[vista];
                return <Icono size={44} aria-hidden="true" />;
              })()}
              <h1>{T.estados[vista].titulo}</h1>
              <p>
                {vista === "pendiente_pago" &&
                !r.pagoEnLinea &&
                home?.contenido.reservas.reservaSinPagoTexto
                  ? home.contenido.reservas.reservaSinPagoTexto
                  : T.estados[vista].detalle}
              </p>
              {verificar.isPending && (
                <p className="reserva-ayuda">{T.verificando}</p>
              )}
              {r.expiraEn && (
                <p className="reserva-ayuda">
                  {T.expiraEn(
                    new Date(r.expiraEn).toLocaleString("es-CO", {
                      dateStyle: "medium",
                      timeStyle: "short",
                    })
                  )}
                </p>
              )}
              {r.motivoEstado && (
                <p className="reserva-ayuda">
                  {T.motivo}: {r.motivoEstado}
                </p>
              )}
              {r.puedePagar && (
                <button
                  type="button"
                  className="button button--solid"
                  onClick={irAPagar}
                  disabled={pagar.isPending}
                >
                  {pagar.isPending
                    ? T.abriendoPago
                    : vista === "pago_rechazado"
                      ? T.reintentar
                      : T.pagar}
                </button>
              )}
            </div>

            <dl className="reserva-card__detalle">
              <div>
                <dt>{T.codigo}</dt>
                <dd className="reserva-codigo">{r.codigo}</dd>
              </div>
              <div>
                <dt>{T.alojamiento}</dt>
                <dd>
                  {r.alojamientoNombre} · {r.proyectoNombre}
                </dd>
              </div>
              <div>
                <dt>{T.fechas}</dt>
                <dd>
                  {formatoDia(r.llegada)} → {formatoDia(r.salida)} (
                  {TEXTOS.reserva.noches(r.noches)})
                </dd>
              </div>
              <div>
                <dt>{T.huespedes}</dt>
                <dd>{r.huespedes}</dd>
              </div>
              <div>
                <dt>{T.subtotal}</dt>
                <dd>{formatoPesos(r.subtotal)}</dd>
              </div>
              {r.tarifaAseo > 0 && (
                <div>
                  <dt>{T.aseo}</dt>
                  <dd>{formatoPesos(r.tarifaAseo)}</dd>
                </div>
              )}
              <div className="reserva-total">
                <dt>{T.total}</dt>
                <dd>
                  {formatoPesos(r.total)} {r.moneda}
                </dd>
              </div>
            </dl>
          </>
        )}

        <Link href="/" className="text-link">
          {T.volver}
        </Link>
      </section>
    </main>
  );
}
