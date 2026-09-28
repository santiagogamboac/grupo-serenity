import { useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { toast } from "sonner";
import { api, mensajeDeError } from "@/lib/api";
import type { SeccionClave } from "@/lib/types";
import type {
  Catalogos,
  ContenidoAdmin,
  Dashboard,
  MediaDetalle,
  Paginado,
  ProyectoAdmin,
  ProyectoInput,
  ResenaAdmin,
  ResenaInput,
  SolicitudAdmin,
  ValorAdmin,
  ValorInput,
} from "./tipos";

const K = {
  dashboard: ["admin", "dashboard"] as const,
  catalogos: ["admin", "catalogos"] as const,
  contenido: ["admin", "contenido"] as const,
  proyectos: ["admin", "proyectos"] as const,
  resenas: ["admin", "resenas"] as const,
  valores: ["admin", "valores"] as const,
  solicitudes: ["admin", "solicitudes"] as const,
  media: ["admin", "media"] as const,
};

/** Cualquier cambio del admin puede alterar lo que ve la landing. */
const PUBLICO: QueryKey = ["public"];

const get = async <T>(url: string, params?: object) => (await api.get<T>(url, { params })).data;

/** Mutación con toast de éxito/error e invalidación de las consultas afectadas. */
function useMutacion<TVars, TRes = unknown>(
  fn: (v: TVars) => Promise<TRes>,
  invalidar: QueryKey[],
  exito?: string
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      invalidar.forEach(k => qc.invalidateQueries({ queryKey: k }));
      qc.invalidateQueries({ queryKey: PUBLICO });
      if (exito) toast.success(exito);
    },
    onError: e => toast.error(mensajeDeError(e)),
  });
}

// ─── Lecturas ─────────────────────────────────────────────────────────────────

export const useDashboard = () => useQuery({ queryKey: K.dashboard, queryFn: () => get<Dashboard>("/admin/dashboard") });

export const useCatalogos = () =>
  useQuery({ queryKey: K.catalogos, queryFn: () => get<Catalogos>("/admin/catalogos"), staleTime: Infinity });

export const useContenido = () =>
  useQuery({ queryKey: K.contenido, queryFn: () => get<ContenidoAdmin>("/admin/contenido") });

export const useProyectos = () =>
  useQuery({ queryKey: K.proyectos, queryFn: () => get<ProyectoAdmin[]>("/admin/proyectos") });

export const useProyecto = (id: number | null) =>
  useQuery({
    queryKey: [...K.proyectos, id],
    queryFn: () => get<ProyectoAdmin>(`/admin/proyectos/${id}`),
    enabled: id !== null,
  });

export const useResenas = () => useQuery({ queryKey: K.resenas, queryFn: () => get<ResenaAdmin[]>("/admin/resenas") });

export const useValores = () => useQuery({ queryKey: K.valores, queryFn: () => get<ValorAdmin[]>("/admin/valores") });

export const useSolicitudes = (f: { estado: string; q: string; page: number }) =>
  useQuery({
    queryKey: [...K.solicitudes, f],
    queryFn: () =>
      get<Paginado<SolicitudAdmin>>("/admin/solicitudes", {
        estado: f.estado || undefined,
        q: f.q || undefined,
        page: f.page,
        pageSize: 15,
      }),
    placeholderData: prev => prev,
  });

export const useMediaLista = (page: number, pageSize = 24) =>
  useQuery({
    queryKey: [...K.media, page, pageSize],
    queryFn: () => get<Paginado<MediaDetalle>>("/admin/media", { page, pageSize }),
    placeholderData: prev => prev,
  });

// ─── Escrituras ───────────────────────────────────────────────────────────────

export const useGuardarSeccion = () =>
  useMutacion(
    ({ clave, cuerpo }: { clave: SeccionClave; cuerpo: object }) => api.put(`/admin/contenido/${clave}`, cuerpo),
    [K.contenido],
    "Cambios guardados"
  );

export const useCrearProyecto = () =>
  useMutacion(async (d: ProyectoInput) => (await api.post<ProyectoAdmin>("/admin/proyectos", d)).data, [K.proyectos]);

export const useActualizarProyecto = () =>
  useMutacion(
    async ({ id, datos }: { id: number; datos: ProyectoInput }) =>
      (await api.put<ProyectoAdmin>(`/admin/proyectos/${id}`, datos)).data,
    [K.proyectos]
  );

export const useEliminarProyecto = () =>
  useMutacion((id: number) => api.delete(`/admin/proyectos/${id}`), [K.proyectos, K.dashboard], "Proyecto eliminado");

export const useReordenarProyectos = () =>
  useMutacion((ids: number[]) => api.put("/admin/proyectos/orden", { ids }), [K.proyectos]);

export const useAgregarImagenProyecto = () =>
  useMutacion(
    ({ id, mediaId, textoAlt }: { id: number; mediaId: number; textoAlt: string }) =>
      api.post(`/admin/proyectos/${id}/imagenes`, { mediaId, textoAlt }),
    [K.proyectos]
  );

export const useAltImagenProyecto = () =>
  useMutacion(
    ({ id, imagenId, textoAlt }: { id: number; imagenId: number; textoAlt: string }) =>
      api.put(`/admin/proyectos/${id}/imagenes/${imagenId}`, { textoAlt }),
    [K.proyectos],
    "Texto alternativo guardado"
  );

export const useEliminarImagenProyecto = () =>
  useMutacion(
    ({ id, imagenId }: { id: number; imagenId: number }) => api.delete(`/admin/proyectos/${id}/imagenes/${imagenId}`),
    [K.proyectos]
  );

export const useReordenarImagenesProyecto = () =>
  useMutacion(
    ({ id, ids }: { id: number; ids: number[] }) => api.put(`/admin/proyectos/${id}/imagenes/orden`, { ids }),
    [K.proyectos]
  );

export const useCrearResena = () =>
  useMutacion((d: ResenaInput) => api.post("/admin/resenas", d), [K.resenas, K.dashboard], "Reseña creada");

export const useActualizarResena = () =>
  useMutacion(
    ({ id, datos }: { id: number; datos: ResenaInput }) => api.put(`/admin/resenas/${id}`, datos),
    [K.resenas, K.dashboard]
  );

export const useEliminarResena = () =>
  useMutacion((id: number) => api.delete(`/admin/resenas/${id}`), [K.resenas, K.dashboard], "Reseña eliminada");

export const useReordenarResenas = () =>
  useMutacion((ids: number[]) => api.put("/admin/resenas/orden", { ids }), [K.resenas]);

export const useCrearValor = () =>
  useMutacion((d: ValorInput) => api.post("/admin/valores", d), [K.valores, K.dashboard], "Valor creado");

export const useActualizarValor = () =>
  useMutacion(
    ({ id, datos }: { id: number; datos: ValorInput }) => api.put(`/admin/valores/${id}`, datos),
    [K.valores, K.dashboard]
  );

export const useEliminarValor = () =>
  useMutacion((id: number) => api.delete(`/admin/valores/${id}`), [K.valores, K.dashboard], "Valor eliminado");

export const useReordenarValores = () =>
  useMutacion((ids: number[]) => api.put("/admin/valores/orden", { ids }), [K.valores]);

export const useActualizarSolicitud = () =>
  useMutacion(
    ({ id, estado, notas }: { id: number; estado: string; notas: string }) =>
      api.put(`/admin/solicitudes/${id}`, { estado, notas }),
    [K.solicitudes, K.dashboard],
    "Solicitud actualizada"
  );

export const useEliminarSolicitud = () =>
  useMutacion((id: number) => api.delete(`/admin/solicitudes/${id}`), [K.solicitudes, K.dashboard], "Solicitud eliminada");

export const useSubirMedia = () =>
  useMutacion(async (archivo: File) => {
    const form = new FormData();
    form.append("archivo", archivo);
    return (await api.post<MediaDetalle>("/admin/media", form)).data;
  }, [K.media]);

export const useEliminarMedia = () =>
  useMutacion((id: number) => api.delete(`/admin/media/${id}`), [K.media], "Imagen eliminada");

export const useCambiarPassword = () =>
  useMutacion(
    (d: { passwordActual: string; passwordNueva: string }) => api.post("/auth/cambiar-password", d),
    [],
    "Contraseña actualizada"
  );
