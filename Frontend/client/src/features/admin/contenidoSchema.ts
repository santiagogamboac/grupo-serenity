import type { SeccionClave } from "@/lib/types";

/**
 * Descripción de los campos editables de cada sección de la landing.
 * El panel genera los formularios a partir de esto; las reglas de validación reales
 * viven en el backend (Serenity.Application/Features/Contenido).
 */
export type TipoCampo = "texto" | "largo" | "titular" | "enlace" | "imagen" | "bool";

export interface CampoSpec {
  nombre: string;
  etiqueta: string;
  tipo: TipoCampo;
  ayuda?: string;
  /** Solo para "imagen": nombre del campo con el id (p. ej. logoMediaId). */
  idCampo?: string;
}

export interface SeccionSpec {
  clave: SeccionClave;
  titulo: string;
  descripcion: string;
  campos: CampoSpec[];
}

const AYUDA_TITULAR = "Enter = salto de línea. *palabra* = cursiva.";

export const SECCIONES: SeccionSpec[] = [
  {
    clave: "marca",
    titulo: "Marca",
    descripcion: "Logo, colibrí animado y textos de identidad (también el título de la pestaña del navegador).",
    campos: [
      { nombre: "nombre", etiqueta: "Nombre de la marca", tipo: "texto" },
      { nombre: "tituloSitio", etiqueta: "Título de la pestaña del navegador", tipo: "texto" },
      { nombre: "leyenda", etiqueta: "Leyenda junto al logo", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "logo", idCampo: "logoMediaId", etiqueta: "Logo", tipo: "imagen", ayuda: "PNG con fondo transparente; se muestra en blanco sobre la portada." },
      { nombre: "logoAlt", etiqueta: "Texto alternativo del logo", tipo: "texto" },
      { nombre: "pajaro", idCampo: "pajaroMediaId", etiqueta: "Colibrí que acompaña el scroll", tipo: "imagen" },
    ],
  },
  {
    clave: "navegacion",
    titulo: "Menú",
    descripcion: "Enlaces del menú superior y botón de reserva. Los enlaces internos empiezan con # (ej. #destinos).",
    campos: [
      { nombre: "ctaTexto", etiqueta: "Texto del botón", tipo: "texto" },
      { nombre: "ctaHref", etiqueta: "Enlace del botón", tipo: "enlace" },
    ],
  },
  {
    clave: "anuncio",
    titulo: "Barra superior",
    descripcion: "La franja oscura sobre el menú.",
    campos: [
      { nombre: "activo", etiqueta: "Mostrar la barra", tipo: "bool" },
      { nombre: "texto", etiqueta: "Texto", tipo: "texto" },
      { nombre: "ctaTexto", etiqueta: "Texto del enlace", tipo: "texto" },
      { nombre: "ctaHref", etiqueta: "Enlace", tipo: "enlace" },
    ],
  },
  {
    clave: "hero",
    titulo: "Portada",
    descripcion: "La primera pantalla del sitio.",
    campos: [
      { nombre: "kicker", etiqueta: "Sobretítulo", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "texto", etiqueta: "Texto", tipo: "largo" },
      { nombre: "ctaPrimarioTexto", etiqueta: "Botón principal — texto", tipo: "texto" },
      { nombre: "ctaPrimarioHref", etiqueta: "Botón principal — enlace", tipo: "enlace" },
      { nombre: "ctaSecundarioTexto", etiqueta: "Enlace secundario — texto", tipo: "texto" },
      { nombre: "ctaSecundarioHref", etiqueta: "Enlace secundario — enlace", tipo: "enlace" },
      { nombre: "imagenPrincipal", idCampo: "imagenPrincipalMediaId", etiqueta: "Imagen principal", tipo: "imagen" },
      { nombre: "imagenPrincipalAlt", etiqueta: "Texto alternativo de la imagen principal", tipo: "texto" },
      { nombre: "imagenSecundaria", idCampo: "imagenSecundariaMediaId", etiqueta: "Imagen secundaria (capa sutil)", tipo: "imagen" },
      { nombre: "imagenSecundariaAlt", etiqueta: "Texto alternativo de la imagen secundaria", tipo: "texto" },
      { nombre: "notaDestinos", etiqueta: "Nota inferior (tras el número de destinos)", tipo: "texto", ayuda: "El número se calcula solo con los proyectos publicados." },
      { nombre: "selloLinea1", etiqueta: "Sello — línea 1", tipo: "texto" },
      { nombre: "selloCentral", etiqueta: "Sello — palabra central", tipo: "texto" },
      { nombre: "selloLinea3", etiqueta: "Sello — línea 3", tipo: "texto" },
    ],
  },
  {
    clave: "intro",
    titulo: "La experiencia",
    descripcion: "Sección de presentación bajo la portada.",
    campos: [
      { nombre: "etiqueta", etiqueta: "Etiqueta", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "lead", etiqueta: "Frase destacada", tipo: "largo" },
      { nombre: "texto", etiqueta: "Texto", tipo: "largo" },
      { nombre: "enlaceTexto", etiqueta: "Enlace — texto", tipo: "texto" },
      { nombre: "enlaceHref", etiqueta: "Enlace — destino", tipo: "enlace" },
      { nombre: "cita", etiqueta: "Cita lateral", tipo: "largo" },
      { nombre: "firma", etiqueta: "Firma de la cita", tipo: "texto" },
    ],
  },
  {
    clave: "valores",
    titulo: "Lo que nos mueve",
    descripcion: "Encabezado de la sección. Las tarjetas se administran en “Valores”.",
    campos: [
      { nombre: "etiqueta", etiqueta: "Etiqueta", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "texto", etiqueta: "Texto lateral", tipo: "largo" },
    ],
  },
  {
    clave: "destinos",
    titulo: "Destinos",
    descripcion: "Encabezado y textos fijos de la sección de destinos. Cada destino se administra en “Proyectos”.",
    campos: [
      { nombre: "etiqueta", etiqueta: "Etiqueta", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "texto", etiqueta: "Texto lateral", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "chipMarca", etiqueta: "Sello sobre la foto", tipo: "texto" },
      { nombre: "franjaEtiqueta", etiqueta: "Franja inferior — etiqueta", tipo: "texto" },
      { nombre: "franjaTitulo", etiqueta: "Franja inferior — título", tipo: "texto" },
    ],
  },
  {
    clave: "resenas",
    titulo: "Reseñas",
    descripcion: "Encabezado y calificación general. Los comentarios se administran en “Reseñas”.",
    campos: [
      { nombre: "etiqueta", etiqueta: "Etiqueta", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "calificacion", etiqueta: "Calificación general", tipo: "texto", ayuda: "Ej.: 4.9" },
      { nombre: "detalle", etiqueta: "Detalle de la calificación", tipo: "texto", ayuda: "Ej.: Basado en 20 reseñas en Google" },
      { nombre: "enlaceTexto", etiqueta: "Enlace — texto", tipo: "texto" },
      { nombre: "enlaceUrl", etiqueta: "Enlace — URL", tipo: "enlace" },
    ],
  },
  {
    clave: "reservas",
    titulo: "Reservas",
    descripcion: "Sección de contacto con el formulario de solicitud.",
    campos: [
      { nombre: "etiqueta", etiqueta: "Etiqueta", tipo: "texto" },
      { nombre: "titulo", etiqueta: "Titular", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "texto", etiqueta: "Texto", tipo: "largo" },
      { nombre: "whatsappTexto", etiqueta: "WhatsApp — texto", tipo: "texto" },
      { nombre: "whatsappUrl", etiqueta: "WhatsApp — enlace", tipo: "enlace", ayuda: "Vacío = no se muestra." },
      { nombre: "nota", etiqueta: "Nota bajo el contacto", tipo: "texto" },
      { nombre: "formTitulo", etiqueta: "Formulario — título", tipo: "texto" },
      { nombre: "formSubtitulo", etiqueta: "Formulario — subtítulo", tipo: "texto" },
      { nombre: "botonTexto", etiqueta: "Botón de envío", tipo: "texto" },
      { nombre: "aviso", etiqueta: "Aviso legal bajo el botón", tipo: "largo" },
      { nombre: "exitoTitulo", etiqueta: "Mensaje de éxito — título", tipo: "texto" },
      { nombre: "exitoDescripcion", etiqueta: "Mensaje de éxito — descripción", tipo: "largo" },
    ],
  },
  {
    clave: "footer",
    titulo: "Pie de página",
    descripcion: "Eslogan, redes sociales y créditos.",
    campos: [
      { nombre: "eslogan", etiqueta: "Eslogan", tipo: "titular", ayuda: AYUDA_TITULAR },
      { nombre: "redesTitulo", etiqueta: "Título de redes", tipo: "texto" },
      { nombre: "facebookUrl", etiqueta: "Facebook", tipo: "enlace", ayuda: "Vacío = no se muestra." },
      { nombre: "instagramUrl", etiqueta: "Instagram", tipo: "enlace" },
      { nombre: "youtubeUrl", etiqueta: "YouTube", tipo: "enlace" },
      { nombre: "copyright", etiqueta: "Copyright", tipo: "texto" },
      { nombre: "frase", etiqueta: "Frase final", tipo: "texto" },
      { nombre: "sitioActualTexto", etiqueta: "Enlace al sitio anterior — texto", tipo: "texto" },
      { nombre: "sitioActualUrl", etiqueta: "Enlace al sitio anterior — URL", tipo: "enlace" },
    ],
  },
];
