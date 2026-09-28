/**
 * Textos de la interfaz (etiquetas de formulario, accesibilidad, mensajes de error).
 * NO son contenido editorial: el contenido de marketing vive en la base de datos y se
 * edita desde el panel admin. Aquí solo quedan las cadenas que pertenecen a la mecánica de la UI.
 */
export const TEXTOS = {
  menuAbrir: "Abrir menú",
  menuCerrar: "Cerrar menú",
  navegacionPrincipal: "Navegación principal",
  inicioAria: (marca: string) => `${marca}, inicio`,
  destinosAria: "Destinos",
  irAReservas: "Ir a reservas",
  cargando: "Cargando…",
  errorTitulo: "No pudimos cargar el sitio",
  errorDetalle: "Revisa tu conexión e inténtalo de nuevo.",
  reintentar: "Reintentar",
  enviando: "Enviando…",
  form: {
    nombre: "Tu nombre",
    nombrePlaceholder: "¿Cómo te llamas?",
    contacto: "Correo o WhatsApp",
    contactoPlaceholder: "tucorreo@ejemplo.com o +57 300 000 0000",
    llegada: "Fecha de llegada",
    huespedes: "Huéspedes",
    destino: "Destino de interés",
    destinoIndeciso: "Aún no lo sé",
    huesped: (n: number) => (n === 1 ? "1 huésped" : `${n} huéspedes`),
    huespedesMas: "4+ huéspedes",
    errorNombre: "Escribe tu nombre.",
    errorContacto: "Déjanos un correo o un número de WhatsApp válido.",
    errorFecha: "Elige una fecha de llegada que no haya pasado.",
    errorEnvio: "No pudimos enviar tu solicitud.",
  },
} as const;
