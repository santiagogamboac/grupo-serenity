import { useMutation, useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { HomeData, SolicitudInput } from "@/lib/types";

export function useHome() {
  return useQuery({
    queryKey: ["public", "home"],
    queryFn: async () => (await api.get<HomeData>("/public/home")).data,
  });
}

export function useCrearSolicitud() {
  return useMutation({
    mutationFn: async (input: SolicitudInput) => (await api.post<{ id: number }>("/public/solicitudes", input)).data,
  });
}
