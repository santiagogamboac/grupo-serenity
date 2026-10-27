import { useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { useLocation } from "wouter";
import { DayPicker } from "react-day-picker";
import { es } from "react-day-picker/locale";
import { CalendarDays, Lock, Users } from "lucide-react";
import { mensajeDeError } from "@/lib/api";
import {
  aIso,
  deIso,
  formatoDia,
  formatoPesos,
  hoyIso,
  nocheOcupada,
  nochesEntre,
  salidaMaxima,
  sumarDias,
} from "@/lib/reservas";
import type {
  AlojamientoPublico,
  ProyectoPublico,
  RangoOcupado,
  ReservasContenido,
} from "@/lib/types";
import type { PedidoDestino } from "./Reservas";
import { TEXTOS } from "../textos";
import { useCotizacion, useCrearReserva, useDisponibilidad } from "../useHome";

const T = TEXTOS.reserva;

const CORREO = /^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/;
const TELEFONO = /^\+?[\d\s\-().]{7,25}$/;

const esquema = z.object({
  nombre: z.string().trim().min(2, TEXTOS.form.errorNombre).max(120),
  correo: z.string().trim().max(200).regex(CORREO, T.errorCorreo),
  telefono: z
    .string()
    .trim()
    .refine(
      v => TELEFONO.test(v) && (v.match(/\d/g)?.length ?? 0) >= 7,
      T.errorTelefono
    ),
  nota: z.string().max(1000).optional(),
  acepto: z.boolean().refine(v => v, T.errorAcepto),
  sitioWeb: z.string().optional(),
});
type Datos = z.infer<typeof esquema>;

interface Props {
  seccion: ReservasContenido;
  proyectos: ProyectoPublico[];
  alojamientos: AlojamientoPublico[];
  pagoEnLinea: boolean;
  pedido?: PedidoDestino;
}

export function ReservaEnLinea({
  seccion,
  proyectos,
  alojamientos,
  pagoEnLinea,
  pedido,
}: Props) {
  const destinos = useMemo(
    () => proyectos.filter(p => alojamientos.some(a => a.proyectoId === p.id)),
    [proyectos, alojamientos]
  );
  const [proyectoId, setProyectoId] = useState(destinos[0]?.id ?? 0);
  const opciones = alojamientos.filter(a => a.proyectoId === proyectoId);
  const [alojamientoId, setAlojamientoId] = useState<number | null>(
    opciones.length === 1 ? opciones[0].id : null
  );
  const aloj = opciones.find(a => a.id === alojamientoId) ?? null;

  const [llegada, setLlegada] = useState<string | null>(null);
  const [salida, setSalida] = useState<string | null>(null);
  const [huespedes, setHuespedes] = useState(2);
  const [, navegar] = useLocation();

  const elegirDestino = (id: number) => {
    setProyectoId(id);
    const deEse = alojamientos.filter(a => a.proyectoId === id);
    setAlojamientoId(deEse.length === 1 ? deEse[0].id : null);
    setLlegada(null);
    setSalida(null);
  };

  // Botón "Ver alojamientos" de un destino: lo preselecciona (si tiene alojamientos publicados).
  useEffect(() => {
    if (pedido && destinos.some(p => p.id === pedido.proyectoId))
      elegirDestino(pedido.proyectoId);
  }, [pedido]);

  const elegirAlojamiento = (a: AlojamientoPublico) => {
    setAlojamientoId(a.id);
    setLlegada(null);
    setSalida(null);
    setHuespedes(h => Math.min(h, a.capacidad));
  };

  const disponibilidad = useDisponibilidad(aloj?.id ?? null);
  const cotizacion = useCotizacion(
    aloj && llegada && salida
      ? {
          alojamientoId: aloj.id,
          llegada,
          salida,
          huespedes: Math.min(huespedes, aloj.capacidad),
        }
      : null
  );
  const crear = useCrearReserva();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Datos>({
    resolver: zodResolver(esquema),
    defaultValues: {
      nombre: "",
      correo: "",
      telefono: "",
      nota: "",
      acepto: false,
      sitioWeb: "",
    },
  });

  const cot = cotizacion.data;
  const listo =
    !!aloj &&
    !!llegada &&
    !!salida &&
    !!cot?.disponible &&
    !cotizacion.isFetching &&
    !cotizacion.isError;

  const enviar = handleSubmit(async d => {
    if (!aloj || !llegada || !salida) {
      toast.error(T.errorFechas);
      return;
    }

    try {
      const r = await crear.mutateAsync({
        alojamientoId: aloj.id,
        llegada,
        salida,
        huespedes,
        nombre: d.nombre.trim(),
        correo: d.correo.trim(),
        telefono: d.telefono.trim(),
        nota: d.nota?.trim() || null,
        sitioWeb: d.sitioWeb ?? "",
      });
      if (!r) return;
      if (r.checkoutUrl) {
        toast.success(T.redirigiendo);
        window.location.assign(r.checkoutUrl);
      } else {
        navegar(`/reserva/${r.codigo}?t=${r.token}`);
      }
    } catch (error) {
      toast.error(T.errorReserva, { description: mensajeDeError(error) });
      // Si alguien tomó las fechas mientras tanto, el calendario debe reflejarlo.
      setSalida(null);
    }
  });

  if (destinos.length === 0) return null;

  return (
    <form className="reserva-form" onSubmit={enviar} noValidate>
      <div className="form-heading">
        <span>{seccion.reservaTitulo}</span>
        {seccion.reservaSubtitulo && (
          <strong className="reserva-subtitulo">
            {pagoEnLinea && <Lock size={18} aria-hidden="true" />}{" "}
            {seccion.reservaSubtitulo}
          </strong>
        )}
      </div>

      <div className="hp-field" aria-hidden="true">
        <label>
          No llenar
          <input
            type="text"
            tabIndex={-1}
            autoComplete="off"
            {...register("sitioWeb")}
          />
        </label>
      </div>

      {destinos.length > 1 && (
        <label>
          {T.destino}
          <select
            value={proyectoId}
            onChange={e => elegirDestino(Number(e.target.value))}
          >
            {destinos.map(p => (
              <option key={p.id} value={p.id}>
                {p.nombre}
              </option>
            ))}
          </select>
        </label>
      )}

      <fieldset className="reserva-bloque">
        <legend>{T.alojamiento}</legend>
        <div
          className="alojamiento-lista"
          role="radiogroup"
          aria-label={T.alojamiento}
        >
          {opciones.map(a => (
            <button
              type="button"
              key={a.id}
              role="radio"
              aria-checked={a.id === alojamientoId}
              className={`alojamiento-opcion${a.id === alojamientoId ? " is-activo" : ""}`}
              onClick={() => elegirAlojamiento(a)}
            >
              {a.imagen && <img src={a.imagen.url} alt="" loading="lazy" />}
              <span className="alojamiento-info">
                <strong>{a.nombre}</strong>
                <span className="alojamiento-meta">
                  <Users size={14} aria-hidden="true" />{" "}
                  {T.capacidad(a.capacidad)}
                  {T.minimo(a.estanciaMinima) &&
                    ` · ${T.minimo(a.estanciaMinima)}`}
                </span>
                <span className="alojamiento-precio">
                  {T.desde} <b>{formatoPesos(a.precioNoche)}</b> {T.porNoche}
                </span>
              </span>
            </button>
          ))}
        </div>
        {aloj?.descripcion && (
          <p className="alojamiento-descripcion">{aloj.descripcion}</p>
        )}
      </fieldset>

      {aloj && (
        <fieldset className="reserva-bloque">
          <legend>
            <CalendarDays size={16} aria-hidden="true" /> {T.fechas}
          </legend>
          {disponibilidad.isPending ? (
            <p className="reserva-ayuda">{T.calendarioCargando}</p>
          ) : disponibilidad.isError ? (
            <p className="reserva-ayuda reserva-ayuda--error">
              {T.calendarioError}
            </p>
          ) : (
            <CalendarioReserva
              ocupado={disponibilidad.data.ocupado}
              estanciaMinima={aloj.estanciaMinima}
              llegada={llegada}
              salida={salida}
              onCambio={(l, s) => {
                setLlegada(l);
                setSalida(s);
              }}
            />
          )}

          <div className="form-row">
            <label>
              {T.huespedes}
              <select
                value={huespedes}
                onChange={e => setHuespedes(Number(e.target.value))}
              >
                {Array.from({ length: aloj.capacidad }, (_, i) => i + 1).map(
                  n => (
                    <option key={n} value={n}>
                      {TEXTOS.form.huesped(n)}
                    </option>
                  )
                )}
              </select>
            </label>
            {llegada && salida && (
              <div className="reserva-noches" aria-live="polite">
                <span>{T.noches(nochesEntre(llegada, salida))}</span>
                <strong>
                  {formatoDia(llegada, { day: "numeric", month: "short" })} →{" "}
                  {formatoDia(salida, { day: "numeric", month: "short" })}
                </strong>
              </div>
            )}
          </div>
        </fieldset>
      )}

      {aloj && llegada && salida && (
        <div
          className="reserva-resumen"
          aria-live="polite"
          aria-busy={cotizacion.isFetching}
        >
          {cotizacion.isError ? (
            <p className="reserva-ayuda reserva-ayuda--error">
              {mensajeDeError(cotizacion.error)}
            </p>
          ) : !cot ? (
            <p className="reserva-ayuda">{T.cotizando}</p>
          ) : !cot.disponible ? (
            <p className="reserva-ayuda reserva-ayuda--error">
              {T.noDisponible}
            </p>
          ) : (
            <dl className={cotizacion.isFetching ? "is-cargando" : ""}>
              {cot.cotizacion.tramos.map(t => (
                <div key={t.desde}>
                  <dt>
                    {T.tramo(
                      t.noches,
                      formatoPesos(t.precioNoche),
                      t.temporada
                    )}
                  </dt>
                  <dd>{formatoPesos(t.noches * t.precioNoche)}</dd>
                </div>
              ))}
              {cot.cotizacion.recargoHuespedes > 0 && (
                <div>
                  <dt>{T.recargo(cot.cotizacion.huespedesExtra)}</dt>
                  <dd>{formatoPesos(cot.cotizacion.recargoHuespedes)}</dd>
                </div>
              )}
              {cot.cotizacion.tarifaAseo > 0 && (
                <div>
                  <dt>{T.aseo}</dt>
                  <dd>{formatoPesos(cot.cotizacion.tarifaAseo)}</dd>
                </div>
              )}
              <div className="reserva-total">
                <dt>{T.total}</dt>
                <dd>{formatoPesos(cot.cotizacion.total)}</dd>
              </div>
            </dl>
          )}
        </div>
      )}

      {listo && (
        <fieldset className="reserva-bloque">
          <legend>{T.tusDatos}</legend>
          <label>
            {T.nombre}
            <input
              autoComplete="name"
              placeholder={TEXTOS.form.nombrePlaceholder}
              {...register("nombre")}
            />
            {errors.nombre && (
              <em className="field-error">{errors.nombre.message}</em>
            )}
          </label>
          <div className="form-row">
            <label>
              {T.correo}
              <input
                type="email"
                autoComplete="email"
                placeholder={T.correoPlaceholder}
                {...register("correo")}
              />
              {errors.correo && (
                <em className="field-error">{errors.correo.message}</em>
              )}
            </label>
            <label>
              {T.telefono}
              <input
                type="tel"
                autoComplete="tel"
                placeholder={T.telefonoPlaceholder}
                {...register("telefono")}
              />
              {errors.telefono && (
                <em className="field-error">{errors.telefono.message}</em>
              )}
            </label>
          </div>
          <label>
            {T.nota}
            <textarea rows={2} maxLength={1000} {...register("nota")} />
          </label>
          {seccion.condiciones && (
            <p className="reserva-condiciones">{seccion.condiciones}</p>
          )}
          <label className="reserva-acepto">
            <input type="checkbox" {...register("acepto")} />
            <span>{T.acepto}</span>
          </label>
          {errors.acepto && (
            <em className="field-error">{errors.acepto.message}</em>
          )}
        </fieldset>
      )}

      <button
        className="button button--solid"
        type="submit"
        disabled={!listo || crear.isPending}
      >
        {crear.isPending
          ? T.reservando
          : seccion.reservaBotonTexto || seccion.botonTexto}
        {cot?.disponible && listo && ` · ${formatoPesos(cot.cotizacion.total)}`}
      </button>
    </form>
  );
}

// ─── Calendario ───────────────────────────────────────────────────────────────

/**
 * Elegir llegada y luego salida. Las noches ocupadas no se pueden tomar, pero su primer día sí sirve de salida
 * (esa noche empieza otra estadía). Totalmente controlado: el rango se pinta con modificadores propios.
 */
export function CalendarioReserva({
  ocupado,
  estanciaMinima,
  llegada,
  salida,
  onCambio,
}: {
  ocupado: RangoOcupado[];
  estanciaMinima: number;
  llegada: string | null;
  salida: string | null;
  onCambio: (llegada: string | null, salida: string | null) => void;
}) {
  const hoy = hoyIso();
  const eligiendoSalida = !!llegada && !salida;
  const tope = llegada ? salidaMaxima(ocupado, llegada) : null;
  const minimaSalida = llegada
    ? sumarDias(llegada, Math.max(1, estanciaMinima))
    : null;

  const deshabilitado = (d: Date) => {
    const iso = aIso(d);
    if (iso < hoy) return true;
    if (eligiendoSalida && iso > llegada!) {
      return iso < minimaSalida! || (tope !== null && iso > tope);
    }
    return nocheOcupada(ocupado, iso);
  };

  const alElegir = (d: Date) => {
    const iso = aIso(d);
    if (eligiendoSalida && iso > llegada!) onCambio(llegada, iso);
    else onCambio(iso, null);
  };

  const mes = llegada ? deIso(llegada) : new Date();
  const ultimoMes = new Date();
  ultimoMes.setMonth(ultimoMes.getMonth() + 12);

  return (
    <div className="calendario-reserva">
      <DayPicker
        locale={es}
        labels={{ labelNext: () => T.mesSiguiente, labelPrevious: () => T.mesAnterior }}
        defaultMonth={mes}
        startMonth={new Date()}
        endMonth={ultimoMes}
        disabled={deshabilitado}
        onDayClick={(d, m) => !m.disabled && alElegir(d)}
        modifiers={{
          ocupado: d => nocheOcupada(ocupado, aIso(d)),
          inicio: d => aIso(d) === llegada,
          fin: d => aIso(d) === salida,
          enRango: d =>
            !!llegada && !!salida && aIso(d) > llegada && aIso(d) < salida,
        }}
        modifiersClassNames={{
          ocupado: "cal-ocupado",
          inicio: "cal-inicio",
          fin: "cal-fin",
          enRango: "cal-en-rango",
        }}
        classNames={{
          root: "cal",
          months: "cal-months",
          month: "cal-month",
          month_caption: "cal-caption",
          caption_label: "cal-caption-label",
          nav: "cal-nav",
          button_previous: "cal-nav-btn",
          button_next: "cal-nav-btn",
          chevron: "cal-chevron",
          month_grid: "cal-grid",
          weekdays: "cal-weekdays",
          weekday: "cal-weekday",
          week: "cal-week",
          day: "cal-day",
          day_button: "cal-day-btn",
          today: "cal-hoy",
          outside: "cal-fuera",
          disabled: "cal-deshabilitado",
          hidden: "cal-oculto",
        }}
        showOutsideDays={false}
      />
      <div className="calendario-pie">
        <span className="reserva-ayuda">
          {eligiendoSalida ? T.elegirSalida : !llegada ? T.elegirLlegada : null}
        </span>
        {llegada && (
          <button
            type="button"
            className="calendario-limpiar"
            onClick={() => onCambio(null, null)}
          >
            {T.cambiarFechas}
          </button>
        )}
        <span className="calendario-leyenda">
          <i aria-hidden="true" /> {T.leyendaOcupado}
        </span>
      </div>
    </div>
  );
}
