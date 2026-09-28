-- V001 — Esquema inicial de Grupo Serenity.
-- Sin enums nativos: varchar + CHECK (ver Task/00-plan-general.md, D3).

CREATE TABLE usuarios (
    id                BIGSERIAL PRIMARY KEY,
    nombre            VARCHAR(120) NOT NULL,
    correo            VARCHAR(200) NOT NULL,
    password_hash     TEXT         NOT NULL,
    rol               VARCHAR(20)  NOT NULL DEFAULT 'admin' CHECK (rol IN ('admin')),
    activo            BOOLEAN      NOT NULL DEFAULT TRUE,
    intentos_fallidos INTEGER      NOT NULL DEFAULT 0,
    bloqueado_hasta   TIMESTAMPTZ  NULL,
    creado_en         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en    TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ux_usuarios_correo ON usuarios (lower(correo));

CREATE TABLE refresh_tokens (
    id          BIGSERIAL PRIMARY KEY,
    usuario_id  BIGINT      NOT NULL REFERENCES usuarios (id) ON DELETE CASCADE,
    token_hash  CHAR(64)    NOT NULL UNIQUE,
    expira_en   TIMESTAMPTZ NOT NULL,
    revocado_en TIMESTAMPTZ NULL,
    creado_en   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_refresh_tokens_usuario ON refresh_tokens (usuario_id);

CREATE TABLE media (
    id              BIGSERIAL PRIMARY KEY,
    storage_key     VARCHAR(300) NOT NULL UNIQUE,
    nombre_original VARCHAR(255) NOT NULL,
    content_type    VARCHAR(100) NOT NULL,
    tamano_bytes    BIGINT       NOT NULL CHECK (tamano_bytes > 0),
    creado_en       TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_media_creado_en ON media (creado_en DESC, id DESC);

-- Secciones editables de la landing (marca, hero, intro, ...), una fila por sección.
CREATE TABLE secciones (
    clave          VARCHAR(50)  PRIMARY KEY,
    contenido      JSONB        NOT NULL,
    actualizado_en TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE proyectos (
    id                 BIGSERIAL PRIMARY KEY,
    slug               VARCHAR(160) NOT NULL UNIQUE,
    nombre             VARCHAR(120) NOT NULL,
    etiqueta           VARCHAR(40)  NOT NULL,
    icono              VARCHAR(30)  NOT NULL,
    ubicacion_etiqueta VARCHAR(120) NOT NULL,
    titulo             VARCHAR(120) NOT NULL,
    descripcion        TEXT         NOT NULL,
    meta_lugar         VARCHAR(120) NOT NULL DEFAULT '',
    meta_estancia      VARCHAR(120) NOT NULL DEFAULT '',
    url_reserva        VARCHAR(500) NULL,
    cta_texto          VARCHAR(60)  NOT NULL DEFAULT 'Ver alojamientos',
    orden              INTEGER      NOT NULL DEFAULT 0,
    publicado          BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en          TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en     TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_proyectos_orden ON proyectos (orden, id);

CREATE TABLE proyecto_imagenes (
    id          BIGSERIAL PRIMARY KEY,
    proyecto_id BIGINT       NOT NULL REFERENCES proyectos (id) ON DELETE CASCADE,
    media_id    BIGINT       NOT NULL REFERENCES media (id) ON DELETE RESTRICT,
    texto_alt   VARCHAR(200) NOT NULL DEFAULT '',
    orden       INTEGER      NOT NULL DEFAULT 0
);
CREATE INDEX ix_proyecto_imagenes_proyecto ON proyecto_imagenes (proyecto_id, orden, id);
CREATE INDEX ix_proyecto_imagenes_media ON proyecto_imagenes (media_id);

CREATE TABLE resenas (
    id             BIGSERIAL PRIMARY KEY,
    autor          VARCHAR(120) NOT NULL,
    contexto       VARCHAR(120) NOT NULL DEFAULT '',
    texto          TEXT         NOT NULL,
    calificacion   SMALLINT     NOT NULL DEFAULT 5 CHECK (calificacion BETWEEN 1 AND 5),
    orden          INTEGER      NOT NULL DEFAULT 0,
    publicada      BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_resenas_orden ON resenas (orden, id);

CREATE TABLE valores (
    id             BIGSERIAL PRIMARY KEY,
    titulo         VARCHAR(80)  NOT NULL,
    descripcion    VARCHAR(300) NOT NULL,
    icono          VARCHAR(30)  NOT NULL,
    variante       VARCHAR(10)  NOT NULL DEFAULT 'normal' CHECK (variante IN ('normal', 'alta', 'ancha')),
    orden          INTEGER      NOT NULL DEFAULT 0,
    activo         BOOLEAN      NOT NULL DEFAULT TRUE,
    creado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_valores_orden ON valores (orden, id);

CREATE TABLE solicitudes (
    id             BIGSERIAL PRIMARY KEY,
    nombre         VARCHAR(120) NOT NULL,
    contacto       VARCHAR(200) NOT NULL,
    fecha_llegada  DATE         NOT NULL,
    huespedes      SMALLINT     NOT NULL CHECK (huespedes BETWEEN 1 AND 20),
    proyecto_id    BIGINT       NULL REFERENCES proyectos (id) ON DELETE SET NULL,
    destino_texto  VARCHAR(160) NOT NULL,
    estado         VARCHAR(20)  NOT NULL DEFAULT 'nueva'
                   CHECK (estado IN ('nueva', 'contactada', 'cerrada', 'descartada')),
    notas          TEXT         NULL,
    creado_en      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    actualizado_en TIMESTAMPTZ  NOT NULL DEFAULT now()
);
CREATE INDEX ix_solicitudes_estado ON solicitudes (estado, creado_en DESC);
CREATE INDEX ix_solicitudes_creado_en ON solicitudes (creado_en DESC, id DESC);

-- Banderas internas (p. ej. 'seed_inicial_v1') para que el seeder no se repita.
CREATE TABLE app_state (
    clave VARCHAR(80) PRIMARY KEY,
    valor TEXT        NOT NULL,
    creado_en TIMESTAMPTZ NOT NULL DEFAULT now()
);
