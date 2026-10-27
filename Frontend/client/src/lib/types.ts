// Tipos del contrato de la API (Task/contrato-api.md).

export interface MediaDto {
  id: number;
  url: string;
  nombreOriginal: string;
}

export interface MarcaContenido {
  nombre: string;
  tituloSitio: string;
  leyenda: string;
  logoAlt: string;
  logo?: MediaDto;
  pajaro?: MediaDto;
  logoMediaId?: number | null;
  pajaroMediaId?: number | null;
}

export interface EnlaceNavegacion {
  texto: string;
  href: string;
}

export interface NavegacionContenido {
  items: EnlaceNavegacion[];
  ctaTexto: string;
  ctaHref: string;
}

export interface AnuncioContenido {
  activo: boolean;
  texto: string;
  ctaTexto: string;
  ctaHref: string;
}

export interface HeroContenido {
  kicker: string;
  titulo: string;
  texto: string;
  ctaPrimarioTexto: string;
  ctaPrimarioHref: string;
  ctaSecundarioTexto: string;
  ctaSecundarioHref: string;
  imagenPrincipalAlt: string;
  imagenSecundariaAlt: string;
  notaDestinos: string;
  selloLinea1: string;
  selloCentral: string;
  selloLinea3: string;
  imagenPrincipal?: MediaDto;
  imagenSecundaria?: MediaDto;
  imagenPrincipalMediaId?: number | null;
  imagenSecundariaMediaId?: number | null;
}

export interface IntroContenido {
  etiqueta: string;
  titulo: string;
  lead: string;
  texto: string;
  enlaceTexto: string;
  enlaceHref: string;
  cita: string;
  firma: string;
}

export interface ValoresContenido {
  etiqueta: string;
  titulo: string;
  texto: string;
}

export interface DestinosContenido {
  etiqueta: string;
  titulo: string;
  texto: string;
  franjaEtiqueta: string;
  franjaTitulo: string;
  chipMarca: string;
}

export interface ResenasContenido {
  etiqueta: string;
  titulo: string;
  calificacion: string;
  detalle: string;
  enlaceTexto: string;
  enlaceUrl: string;
}

export interface ReservasContenido {
  etiqueta: string;
  titulo: string;
  texto: string;
  whatsappTexto: string;
  whatsappUrl: string;
  nota: string;
  formTitulo: string;
  formSubtitulo: string;
  botonTexto: string;
  aviso: string;
  exitoTitulo: string;
  exitoDescripcion: string;
  reservaTitulo: string;
  reservaSubtitulo: string;
  reservaBotonTexto: string;
  condiciones: string;
  reservaSinPagoTexto: string;
}

export interface FooterContenido {
  eslogan: string;
  redesTitulo: string;
  facebookUrl: string;
  instagramUrl: string;
  youtubeUrl: string;
  copyright: string;
  frase: string;
}

export interface Contenido {
  marca: MarcaContenido;
  navegacion: NavegacionContenido;
  anuncio: AnuncioContenido;
  hero: HeroContenido;
  intro: IntroContenido;
  valores: ValoresContenido;
  destinos: DestinosContenido;
  resenas: ResenasContenido;
  reservas: ReservasContenido;
  footer: FooterContenido;
}

export type SeccionClave = keyof Contenido;

export interface ProyectoImagenPublica {
  id: number;
  url: string;
  textoAlt: string;
}

export interface ProyectoPublico {
  id: number;
  slug: string;
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
  imagenes: ProyectoImagenPublica[];
}

export interface ValorPublico {
  id: number;
  titulo: string;
  descripcion: string;
  icono: string;
  variante: "normal" | "alta" | "ancha";
}

export interface ResenaPublica {
  id: number;
  autor: string;
  contexto: string;
  texto: string;
  calificacion: number;
}

export interface HomeData {
  contenido: Contenido;
  proyectos: ProyectoPublico[];
  valores: ValorPublico[];
  resenas: ResenaPublica[];
  reservaDirecta: ReservaDirecta;
}

export interface SolicitudInput {
  nombre: string;
  contacto: string;
  fechaLlegada: string;
  huespedes: number;
  proyectoId: number | null;
  destinoTexto: string | null;
  sitioWeb: string;
}

// ─── Reserva directa (alojamientos con precio + pago con Wompi) ───────────────
// Fechas de calendario como "yyyy-MM-dd".

export interface AlojamientoPublico {
  id: number;
  proyectoId: number;
  nombre: string;
  descripcion: string;
  imagen: MediaDto | null;
  capacidad: number;
  huespedesIncluidos: number;
  precioNoche: number;
  recargoHuespedExtra: number;
  tarifaAseo: number;
  estanciaMinima: number;
  /** Galería (sin la portada `imagen`), en orden. */
  imagenes: ImagenAlojamiento[];
}

export interface ImagenAlojamiento {
  id: number;
  url: string;
  textoAlt: string;
}

export interface ReservaDirecta {
  pagoEnLinea: boolean;
  alojamientos: AlojamientoPublico[];
}

/** Noches ocupadas: [desde, hasta). */
export interface RangoOcupado {
  desde: string;
  hasta: string;
}

export interface DisponibilidadAlojamiento {
  alojamientoId: number;
  desde: string;
  hasta: string;
  ocupado: RangoOcupado[];
}

export interface TramoPrecio {
  desde: string;
  hasta: string;
  noches: number;
  precioNoche: number;
  temporada: string | null;
}

export interface Cotizacion {
  alojamientoId: number;
  llegada: string;
  salida: string;
  huespedes: number;
  noches: number;
  tramos: TramoPrecio[];
  huespedesExtra: number;
  recargoHuespedes: number;
  subtotal: number;
  tarifaAseo: number;
  total: number;
  moneda: string;
}

export interface CotizacionPublica {
  cotizacion: Cotizacion;
  disponible: boolean;
}

export interface CotizarInput {
  alojamientoId: number;
  llegada: string;
  salida: string;
  huespedes: number;
}

export interface ReservaInput extends CotizarInput {
  nombre: string;
  correo: string;
  telefono: string;
  nota: string | null;
  sitioWeb: string;
}

export interface ReservaCreada {
  codigo: string;
  token: string;
  total: number;
  pagoEnLinea: boolean;
  checkoutUrl: string | null;
  expiraEn: string | null;
}

export type EstadoReserva = "pendiente_pago" | "confirmada" | "cancelada" | "expirada" | "revision";

export interface ReservaPublica {
  codigo: string;
  estado: EstadoReserva;
  motivoEstado: string | null;
  alojamientoNombre: string;
  proyectoNombre: string;
  llegada: string;
  salida: string;
  noches: number;
  huespedes: number;
  nombre: string;
  subtotal: number;
  tarifaAseo: number;
  total: number;
  moneda: string;
  expiraEn: string | null;
  pagoEnLinea: boolean;
  /** Estado de la última transacción en Wompi (PENDING, APPROVED, DECLINED, VOIDED, ERROR). */
  pagoEstado: string | null;
  puedePagar: boolean;
}
