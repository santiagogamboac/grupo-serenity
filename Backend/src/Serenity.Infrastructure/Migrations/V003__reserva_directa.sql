-- Reserva directa con pago en línea (reemplazo de WP Hotel Booking + WooPayments).
-- Dinero en pesos colombianos enteros (BIGINT). Rangos de fechas [desde, hasta): hasta = día de salida.
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Unidad reservable de un proyecto (cabaña, habitación, casa completa).
CREATE TABLE alojamientos (
    id                    BIGSERIAL PRIMARY KEY,
    proyecto_id           BIGINT       NOT NULL REFERENCES proyectos (id) ON DELETE CASCADE,
    nombre                VARCHAR(120) NOT NULL,
    descripcion           TEXT         NOT NULL DEFAULT '',
    imagen_media_id       BIGINT       NULL REFERENCES media (id) ON DELETE RESTRICT,
    capacidad             SMALLINT     NOT NULL CHECK (capacidad BETWEEN 1 AND 50),
    huespedes_incluidos   SMALLINT     NOT NULL CHECK (huespedes_incluidos >= 1),
    precio_noche          BIGINT       NOT NULL CHECK (precio_noche > 0),
    recargo_huesped_extra BIGINT       NOT NULL DEFAULT 0 CHECK (recargo_huesped_extra >= 0),
    tarifa_aseo           BIGINT       NOT NULL DEFAULT 0 CHECK (tarifa_aseo >= 0),
    estancia_minima       SMALLINT     NOT NULL DEFAULT 1 CHECK (estancia_minima BETWEEN 1 AND 60),
    -- Si el alojamiento también se publica en Airbnb, sus bloqueos iCal lo ocupan.
    anuncio_id            BIGINT       NULL UNIQUE REFERENCES anuncios (id) ON DELETE SET NULL,
    -- Secreto del feed .ics de salida (se pega en Airbnb → Importar calendario).
    token_ical            VARCHAR(64)  NOT NULL UNIQUE,
    orden                 INTEGER      NOT NULL DEFAULT 0,
    publicado             BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en             TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CHECK (huespedes_incluidos <= capacidad)
);
CREATE INDEX ix_alojamientos_proyecto ON alojamientos (proyecto_id, orden, id);

-- Precio por noche distinto en un rango (temporada alta, festivos). No se solapan.
CREATE TABLE alojamiento_tarifas (
    id             BIGSERIAL PRIMARY KEY,
    alojamiento_id BIGINT      NOT NULL REFERENCES alojamientos (id) ON DELETE CASCADE,
    nombre         VARCHAR(80) NOT NULL,
    desde          DATE        NOT NULL,
    hasta          DATE        NOT NULL,
    precio_noche   BIGINT      NOT NULL CHECK (precio_noche > 0),
    CHECK (hasta > desde),
    EXCLUDE USING gist (alojamiento_id WITH =, daterange(desde, hasta) WITH &&)
);

-- Noches cerradas a mano (mantenimiento, uso propio, reservas tomadas por teléfono).
CREATE TABLE alojamiento_bloqueos (
    id             BIGSERIAL PRIMARY KEY,
    alojamiento_id BIGINT       NOT NULL REFERENCES alojamientos (id) ON DELETE CASCADE,
    desde          DATE         NOT NULL,
    hasta          DATE         NOT NULL,
    motivo         VARCHAR(200) NOT NULL DEFAULT '',
    creado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CHECK (hasta > desde)
);
CREATE INDEX ix_alojamiento_bloqueos ON alojamiento_bloqueos (alojamiento_id, desde);

CREATE TABLE reservas (
    id                  BIGSERIAL PRIMARY KEY,
    codigo              VARCHAR(20)  NOT NULL UNIQUE,
    -- Secreto del enlace de consulta (/reserva/{codigo}?t=...). Se guarda en claro: se reenvía en los correos.
    token_consulta      VARCHAR(64)  NOT NULL,
    -- Una reserva conserva su historia: no se puede borrar un alojamiento que tenga reservas.
    alojamiento_id      BIGINT       NOT NULL REFERENCES alojamientos (id) ON DELETE RESTRICT,
    llegada             DATE         NOT NULL,
    salida              DATE         NOT NULL,
    huespedes           SMALLINT     NOT NULL CHECK (huespedes BETWEEN 1 AND 50),
    nombre              VARCHAR(120) NOT NULL,
    correo              VARCHAR(200) NOT NULL,
    telefono            VARCHAR(40)  NOT NULL,
    nota_huesped        VARCHAR(1000) NULL,
    noches              SMALLINT     NOT NULL CHECK (noches > 0),
    subtotal            BIGINT       NOT NULL CHECK (subtotal >= 0),
    tarifa_aseo         BIGINT       NOT NULL CHECK (tarifa_aseo >= 0),
    total               BIGINT       NOT NULL CHECK (total > 0),
    moneda              CHAR(3)      NOT NULL DEFAULT 'COP',
    estado              VARCHAR(20)  NOT NULL DEFAULT 'pendiente_pago'
                        CHECK (estado IN ('pendiente_pago', 'confirmada', 'cancelada', 'expirada', 'revision')),
    motivo_estado       VARCHAR(300) NULL,
    -- Mientras está pendiente, las fechas quedan retenidas hasta aquí.
    expira_en           TIMESTAMPTZ  NULL,
    pago_proveedor      VARCHAR(20)  NULL CHECK (pago_proveedor IN ('wompi', 'manual')),
    pago_intentos       INTEGER      NOT NULL DEFAULT 0,
    pago_referencia     VARCHAR(40)  NULL,
    pago_transaccion_id VARCHAR(80)  NULL,
    pago_estado         VARCHAR(20)  NULL,
    pago_metodo         VARCHAR(40)  NULL,
    pagado_en           TIMESTAMPTZ  NULL,
    notas               TEXT         NULL,
    creado_en           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CHECK (salida > llegada),
    CHECK (total = subtotal + tarifa_aseo),
    -- Red de seguridad contra la doble reserva (la aplicación además serializa con un advisory lock).
    EXCLUDE USING gist (alojamiento_id WITH =, daterange(llegada, salida) WITH &&)
        WHERE (estado IN ('pendiente_pago', 'confirmada'))
);
CREATE INDEX ix_reservas_estado ON reservas (estado, creado_en DESC);
CREATE INDEX ix_reservas_creado_en ON reservas (creado_en DESC, id DESC);
CREATE INDEX ix_reservas_alojamiento ON reservas (alojamiento_id, llegada);
