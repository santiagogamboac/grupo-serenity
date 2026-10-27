import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  CotizacionPublica,
  CotizarInput,
  DisponibilidadAlojamiento,
  HomeData,
  ReservaCreada,
  ReservaInput,
  ReservaPublica,
  SolicitudInput,
} from "@/lib/types";

export function useHome() {
  return useQuery({
    queryKey: ["public", "home"],
    queryFn: async () => (await api.get<HomeData>("/public/home")).data,
  });
}

export function useCrearSolicitud() {
  return useMutation({
    mutationFn: async (input: SolicitudInput) => (await api.post<{ id: number }>("/public/solicitudes", input)).data,
  });
}

// ─── Reserva directa ──────────────────────────────────────────────────────────

export function useDisponibilidad(alojamientoId: number | null) {
  return useQuery({
    queryKey: ["public", "disponibilidad", alojamientoId],
    queryFn: async () =>
      (await api.get<DisponibilidadAlojamiento>(`/public/alojamientos/${alojamientoId}/disponibilidad`)).data,
    enabled: alojamientoId !== null,
    staleTime: 60_000,
  });
}

export function useCotizacion(input: CotizarInput | null) {
  return useQuery({
    queryKey: ["public", "cotizacion", input],
    queryFn: async () => (await api.post<CotizacionPublica>("/public/reservas/cotizar", input)).data,
    enabled: input !== null,
    placeholderData: keepPreviousData,
    retry: false,
  });
}

export function useCrearReserva() {
  const qc = useQueryClient();
  return useMutation({
    // 202 sin cuerpo = descartada en silencio (honeypot): devuelve null.
    mutationFn: async (input: ReservaInput) => (await api.post<ReservaCreada | "">("/public/reservas", input)).data || null,
    // Las fechas cambian de dueño (o alguien las tomó antes): el calendario se vuelve a pedir.
    onSettled: () => qc.invalidateQueries({ queryKey: ["public", "disponibilidad"] }),
  });
}

const claveReserva = (codigo: string) => ["public", "reserva", codigo];
const paramsToken = (token: string | null) => ({ params: { t: token } });

/** Estado de una reserva. Mientras Wompi tiene el pago en proceso, se consulta cada 5 s. */
export function useReserva(codigo: string, token: string | null) {
  return useQuery({
    queryKey: claveReserva(codigo),
    queryFn: async () => (await api.get<ReservaPublica>(`/public/reservas/${codigo}`, paramsToken(token))).data,
    enabled: !!codigo && !!token,
    retry: false,
    refetchInterval: q =>
      q.state.data?.estado === "pendiente_pago" && q.state.data.pagoEstado === "PENDING" ? 5_000 : false,
  });
}

/** Al volver de Wompi: el servidor consulta la transacción y la aplica. */
export function useVerificarPago(codigo: string, token: string | null) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (transaccionId: string) =>
      (await api.post<ReservaPublica>(`/public/reservas/${codigo}/verificar`, { transaccionId }, paramsToken(token))).data,
    onSuccess: r => qc.setQueryData(claveReserva(codigo), r),
  });
}

/** Nuevo intento de pago (p. ej. tras un rechazo): devuelve la URL del checkout de Wompi. */
export function useNuevoPago(codigo: string, token: string | null) {
  return useMutation({
    mutationFn: async () =>
      (await api.post<{ checkoutUrl: string }>(`/public/reservas/${codigo}/pago`, null, paramsToken(token))).data,
  });
}
