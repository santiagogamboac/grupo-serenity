/**
 * Formato de titulares editables (ver Task/00-plan-general.md, D12):
 *   "\n"      → salto de línea
 *   "*texto*" → énfasis (<em>)
 */
export type RichSegment = { text: string; em: boolean };
export type RichLine = RichSegment[];

const EM = /\*([^*\n]+)\*/g;

export function parseRich(texto: string): RichLine[] {
  return texto.split(/\r?\n/).map(linea => {
    const segmentos: RichSegment[] = [];
    let ultimo = 0;

    for (const m of Array.from(linea.matchAll(EM))) {
      const inicio = m.index ?? 0;
      if (inicio > ultimo) segmentos.push({ text: linea.slice(ultimo, inicio), em: false });
      segmentos.push({ text: m[1], em: true });
      ultimo = inicio + m[0].length;
    }

    if (ultimo < linea.length) segmentos.push({ text: linea.slice(ultimo), em: false });
    return segmentos;
  });
}

/** Texto plano (sin marcas) para usos como atributos o <title>. */
export function plainRich(texto: string): string {
  return parseRich(texto)
    .map(l => l.map(s => s.text).join(""))
    .join(" ");
}

export const pad2 = (n: number) => String(n).padStart(2, "0");
