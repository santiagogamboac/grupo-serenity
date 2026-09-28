import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios";

/**
 * Cliente HTTP único. Todo va a /api (Vite lo proxya en desarrollo; nginx en docker).
 * El access token vive en memoria; el refresh token en localStorage para sobrevivir recargas.
 */
export const api = axios.create({ baseURL: "/api", timeout: 30_000 });

const REFRESH_KEY = "serenity_refresh_token";
let accessToken: string | null = null;

export const sesionTokens = {
  get access() {
    return accessToken;
  },
  get refresh(): string | null {
    try {
      return localStorage.getItem(REFRESH_KEY);
    } catch {
      return null;
    }
  },
  set(access: string, refresh: string) {
    accessToken = access;
    try {
      localStorage.setItem(REFRESH_KEY, refresh);
    } catch {
      /* almacenamiento no disponible: la sesión dura lo que dure la pestaña */
    }
  },
  clear() {
    accessToken = null;
    try {
      localStorage.removeItem(REFRESH_KEY);
    } catch {
      /* noop */
    }
  },
};

export const SESION_EXPIRADA = "serenity:sesion-expirada";

interface TokensResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

// Un solo refresh en vuelo: las peticiones que fallan a la vez esperan la misma promesa.
let refrescando: Promise<string> | null = null;

export function refrescarSesion(): Promise<string> {
  refrescando ??= (async () => {
    const refresh = sesionTokens.refresh;
    if (!refresh) throw new Error("Sin refresh token");
    // axios "pelado": no pasa por los interceptores (evita bucles).
    const { data } = await axios.post<TokensResponse>("/api/auth/refresh", { refreshToken: refresh });
    sesionTokens.set(data.accessToken, data.refreshToken);
    return data.accessToken;
  })().finally(() => {
    refrescando = null;
  });
  return refrescando;
}

api.interceptors.request.use(config => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`;
  return config;
});

type Reintentable = InternalAxiosRequestConfig & { _reintentada?: boolean };

api.interceptors.response.use(
  r => r,
  async (error: AxiosError) => {
    const config = error.config as Reintentable | undefined;
    const esAuth = config?.url?.startsWith("/auth/login") || config?.url?.startsWith("/auth/refresh");

    if (error.response?.status === 401 && config && !config._reintentada && !esAuth && sesionTokens.refresh) {
      config._reintentada = true;
      try {
        const nuevo = await refrescarSesion();
        config.headers.Authorization = `Bearer ${nuevo}`;
        return api(config);
      } catch {
        sesionTokens.clear();
        window.dispatchEvent(new Event(SESION_EXPIRADA));
      }
    }

    return Promise.reject(error);
  }
);

/** Mensaje legible a partir de un ProblemDetails de la API (o de un fallo de red). */
export function mensajeDeError(error: unknown, porDefecto = "Ocurrió un error. Inténtalo de nuevo."): string {
  if (axios.isAxiosError(error)) {
    if (!error.response) return "No pudimos conectar con el servidor. Revisa tu conexión.";
    if (error.response.status === 429) return "Demasiadas solicitudes. Espera un minuto e inténtalo de nuevo.";

    const d = error.response.data as
      | { detail?: string; errors?: { propertyName?: string; errorMessage?: string }[] }
      | undefined;
    if (d?.errors?.length) return d.errors.map(e => e.errorMessage).filter(Boolean).join(" ");
    if (d?.detail) return d.detail;
  }
  return porDefecto;
}
