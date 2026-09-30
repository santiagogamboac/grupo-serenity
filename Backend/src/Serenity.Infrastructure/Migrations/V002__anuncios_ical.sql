-- Anuncios de Airbnb por proyecto. Cada anuncio tiene su propio iCal (secreto).
CREATE TABLE anuncios (
    id                 BIGSERIAL PRIMARY KEY,
    proyecto_id        BIGINT       NOT NULL REFERENCES proyectos (id) ON DELETE CASCADE,
    nombre             VARCHAR(120) NOT NULL,
    airbnb_room_id     VARCHAR(30)  NOT NULL UNIQUE,
    url_anuncio        VARCHAR(500) NOT NULL,
    ical_url           VARCHAR(600) NULL,
    orden              INTEGER      NOT NULL DEFAULT 0,
    publicado          BOOLEAN      NOT NULL DEFAULT TRUE,
    ultima_sync_intento TIMESTAMPTZ NULL,
    ultima_sync_ok     TIMESTAMPTZ  NULL,
    ultima_sync_error  TEXT         NULL,
    creado_en          TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en     TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_anuncios_proyecto ON anuncios (proyecto_id, orden, id);

-- Noches bloqueadas: [desde, hasta) (hasta = día de salida, exclusivo, como DTEND de iCal).
CREATE TABLE disponibilidad_bloqueos (
    id          BIGSERIAL PRIMARY KEY,
    anuncio_id  BIGINT      NOT NULL REFERENCES anuncios (id) ON DELETE CASCADE,
    desde       DATE        NOT NULL,
    hasta       DATE        NOT NULL,
    origen      VARCHAR(20) NOT NULL DEFAULT 'airbnb' CHECK (origen IN ('airbnb', 'manual')),
    uid_externo VARCHAR(200) NULL,
    creado_en   TIMESTAMPTZ NOT NULL DEFAULT now(),
    CHECK (hasta > desde)
);
CREATE INDEX ix_bloqueos_anuncio ON disponibilidad_bloqueos (anuncio_id, desde);
