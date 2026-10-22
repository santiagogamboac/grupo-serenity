import type { RangoOcupado } from "./types";

// Fechas de calendario como "yyyy-MM-dd" (sin hora ni zona): es lo que maneja la API y se compara como texto.

/** Date local → "yyyy-MM-dd". */
export const aIso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

/** "yyyy-MM-dd" → Date local a medianoche. */
export const deIso = (iso: string) => {
  const [y, m, d] = iso.split("-").map(Number);
  return new Date(y, m - 1, d);
};

export const sumarDias = (iso: string, dias: number) => {
  const d = deIso(iso);
  d.setDate(d.getDate() + dias);
  return aIso(d);
};

export const hoyIso = () => aIso(new Date());

export const nochesEntre = (llegada: string, salida: string) =>
  Math.round((deIso(salida).getTime() - deIso(llegada).getTime()) / 86_400_000);

/** ¿La noche que empieza en `dia` está ocupada? */
export const nocheOcupada = (ocupado: RangoOcupado[], dia: string) =>
  ocupado.some(r => r.desde <= dia && dia < r.hasta);

/**
 * Última salida posible partiendo de `llegada`: el inicio del primer tramo ocupado posterior (ese día se puede salir,
 * porque la noche ocupada empieza ahí). null si no hay nada ocupado después.
 */
export const salidaMaxima = (ocupado: RangoOcupado[], llegada: string) =>
  ocupado
    .filter(r => r.desde > llegada)
    .map(r => r.desde)
    .sort()[0] ?? null;

/** ¿Ninguna noche de [llegada, salida) está ocupada? */
export const rangoLibre = (
  ocupado: RangoOcupado[],
  llegada: string,
  salida: string
) => !ocupado.some(r => r.desde < salida && llegada < r.hasta);

const PESOS = new Intl.NumberFormat("es-CO", {
  style: "currency",
  currency: "COP",
  maximumFractionDigits: 0,
});

/** $ 1.250.000 */
export const formatoPesos = (valor: number) => PESOS.format(valor);

/** "vie, 8 de jun de 2027" (o el formato corto que dé el navegador en es-CO). */
export const formatoDia = (
  iso: string,
  opciones: Intl.DateTimeFormatOptions = {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: "numeric",
  }
) => deIso(iso).toLocaleDateString("es-CO", opciones);
