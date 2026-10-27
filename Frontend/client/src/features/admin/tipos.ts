import type { Contenido, MediaDto } from "@/lib/types";

export interface UsuarioAdmin {
  id: number;
  nombre: string;
  correo: string;
  rol: string;
}

export interface LoginResponse {
  user: UsuarioAdmin;
  tokens: { accessToken: string; refreshToken: string; expiresIn: number };
}

export interface Paginado<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Dashboard {
  solicitudesNuevas: number;
  solicitudesTotal: number;
  proyectosPublicados: number;
  proyectosTotal: number;
  resenasPublicadas: number;
  valoresActivos: number;
  reservasPendientesPago: number;
  reservasEnRevision: number;
  reservasProximas: number;
}

export interface Catalogos {
  iconos: string[];
  variantesValor: string[];
  estadosSolicitud: string[];
  estadosReserva: string[];
  secciones: string[];
}

export interface MediaDetalle extends MediaDto {
  contentType: string;
  tamanoBytes: number;
  creadoEn: string;
}

export interface ProyectoImagenAdmin {
  id: number;
  mediaId: number;
  url: string;
  textoAlt: string;
  orden: number;
}

export interface ProyectoInput {
  nombre: string;
  etiqueta: string;
  icono: string;
  ubicacionEtiqueta: string;
  titulo: string;
  descripcion: string;
  metaLugar: string;
  metaEstancia: string;
  urlReserva: string | null;
  ctaTexto: string;
  publicado: boolean;
}

export interface ProyectoAdmin extends ProyectoInput {
  id: number;
  slug: string;
  orden: number;
  imagenes: ProyectoImagenAdmin[];
}

export interface ResenaInput {
  autor: string;
  contexto: string;
  texto: string;
  calificacion: number;
  publicada: boolean;
}

export interface ResenaAdmin extends ResenaInput {
  id: number;
  orden: number;
}

export interface ValorInput {
  titulo: string;
  descripcion: string;
  icono: string;
  variante: string;
  activo: boolean;
}

export interface ValorAdmin extends ValorInput {
  id: number;
  orden: number;
}

export interface SolicitudAdmin {
  id: number;
  nombre: string;
  contacto: string;
  fechaLlegada: string;
  huespedes: number;
  proyectoId: number | null;
  destinoTexto: string;
  estado: string;
  notas: string | null;
  creadoEn: string;
  actualizadoEn: string;
}

/** Contenido del admin: igual al público pero con los ids de imagen a la vista. */
export type ContenidoAdmin = Contenido;

export interface AnuncioInput {
  nombre: string;
  urlAnuncio: string;
  icalUrl: string | null;
  publicado: boolean;
}

export interface AnuncioAdmin extends AnuncioInput {
  id: number;
  proyectoId: number;
  airbnbRoomId: string;
  ultimaSyncIntento: string | null;
  ultimaSyncOk: string | null;
  ultimaSyncError: string | null;
}

// ─── Reserva directa ──────────────────────────────────────────────────────────
// Fechas de calendario como "yyyy-MM-dd"; montos en pesos (COP, sin decimales).

export interface AlojamientoInput {
  nombre: string;
  descripcion: string;
  imagenMediaId: number | null;
  capacidad: number;
  huespedesIncluidos: number;
  precioNoche: number;
  recargoHuespedExtra: number;
  tarifaAseo: number;
  estanciaMinima: number;
  anuncioId: number | null;
  publicado: boolean;
}

export interface TarifaInput {
  nombre: string;
  desde: string;
  hasta: string;
  precioNoche: number;
}

export interface Tarifa extends TarifaInput {
  id: number;
}

export interface AlojamientoAdmin extends AlojamientoInput {
  id: number;
  proyectoId: number;
  imagen: MediaDto | null;
  orden: number;
  /** Feed .ics de salida para pegar en Airbnb ("Importar calendario"). */
  urlIcal: string;
  tarifas: Tarifa[];
}

export interface BloqueoInput {
  desde: string;
  hasta: string;
  motivo: string | null;
}

export interface BloqueoManual {
  id: number;
  desde: string;
  hasta: string;
  motivo: string;
  creadoEn: string;
}

export interface ReservaAdmin {
  id: number;
  codigo: string;
  alojamientoId: number;
  alojamientoNombre: string;
  proyectoId: number;
  proyectoNombre: string;
  llegada: string;
  salida: string;
  noches: number;
  huespedes: number;
  nombre: string;
  correo: string;
  telefono: string;
  notaHuesped: string | null;
  subtotal: number;
  tarifaAseo: number;
  total: number;
  moneda: string;
  estado: string;
  motivoEstado: string | null;
  expiraEn: string | null;
  pagoProveedor: string | null;
  pagoIntentos: number;
  pagoReferencia: string | null;
  pagoTransaccionId: string | null;
  pagoEstado: string | null;
  pagoMetodo: string | null;
  pagadoEn: string | null;
  notas: string | null;
  /** Enlace /reserva/CODIGO?t=TOKEN para compartir con el huésped. */
  enlaceHuesped: string;
  creadoEn: string;
  actualizadoEn: string;
}
