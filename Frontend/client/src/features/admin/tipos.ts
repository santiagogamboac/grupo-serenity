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
}

export interface Catalogos {
  iconos: string[];
  variantesValor: string[];
  estadosSolicitud: string[];
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
