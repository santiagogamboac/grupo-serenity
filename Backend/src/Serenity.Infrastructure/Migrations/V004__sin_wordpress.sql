-- El sitio ya no depende del WordPress anterior: se quitan los enlaces que apuntaban a él en bases ya sembradas.
-- Idempotente (en una base nueva no cambia nada: el seed ya viene limpio).

-- Pie de página: el enlace "Visitar sitio actual" dejó de existir.
UPDATE secciones
SET contenido = contenido - 'sitioActualTexto' - 'sitioActualUrl',
    actualizado_en = now()
WHERE clave = 'footer'
  AND (contenido ? 'sitioActualTexto' OR contenido ? 'sitioActualUrl');

-- Proyectos: los enlaces de reserva a la tienda del WordPress (/shop/). Sin URL, el botón del destino lleva a la
-- reserva en línea (#reservas) cuando el proyecto tiene alojamientos publicados.
UPDATE proyectos
SET url_reserva = NULL,
    actualizado_en = now()
WHERE url_reserva ILIKE '%gruposerenity.com%';
