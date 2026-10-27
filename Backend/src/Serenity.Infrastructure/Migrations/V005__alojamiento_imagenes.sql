-- Galería de fotos de cada alojamiento (además de alojamientos.imagen_media_id, que sigue siendo la portada).
-- Mismo patrón que proyecto_imagenes: el media vive en la biblioteca y no se puede borrar mientras esté en uso.
CREATE TABLE alojamiento_imagenes (
    id             BIGSERIAL PRIMARY KEY,
    alojamiento_id BIGINT       NOT NULL REFERENCES alojamientos (id) ON DELETE CASCADE,
    media_id       BIGINT       NOT NULL REFERENCES media (id) ON DELETE RESTRICT,
    texto_alt      VARCHAR(200) NOT NULL DEFAULT '',
    orden          INTEGER      NOT NULL DEFAULT 0
);
CREATE INDEX ix_alojamiento_imagenes_alojamiento ON alojamiento_imagenes (alojamiento_id, orden, id);
CREATE INDEX ix_alojamiento_imagenes_media ON alojamiento_imagenes (media_id);
