import {
  Bird,
  Coffee,
  Compass,
  Heart,
  HeartHandshake,
  Home,
  Leaf,
  MapPin,
  Mountain,
  Sailboat,
  ShieldCheck,
  Sparkles,
  Star,
  Sun,
  Tent,
  Trees,
  Users,
  Waves,
  type LucideIcon,
} from "lucide-react";

/**
 * Claves de icono que acepta la API (Serenity.Domain.Catalogos.Iconos).
 * Si se agrega una clave en el backend, se agrega aquí su componente.
 */
export const ICONOS: Record<string, LucideIcon> = {
  "heart-handshake": HeartHandshake,
  sparkles: Sparkles,
  "shield-check": ShieldCheck,
  "map-pin": MapPin,
  waves: Waves,
  mountain: Mountain,
  sun: Sun,
  trees: Trees,
  tent: Tent,
  sailboat: Sailboat,
  compass: Compass,
  leaf: Leaf,
  heart: Heart,
  star: Star,
  home: Home,
  users: Users,
  bird: Bird,
  coffee: Coffee,
};

export const iconoDe = (clave: string): LucideIcon => ICONOS[clave] ?? Sparkles;
