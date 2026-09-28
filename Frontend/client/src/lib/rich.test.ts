import { describe, expect, it } from "vitest";
import { pad2, parseRich, plainRich } from "./rich";

describe("parseRich", () => {
  it("separa líneas por salto de línea", () => {
    expect(parseRich("Hospitalidad\ncon intención")).toEqual([
      [{ text: "Hospitalidad", em: false }],
      [{ text: "con intención", em: false }],
    ]);
  });

  it("marca el énfasis con asteriscos", () => {
    expect(parseRich("El descanso\n*también* es un destino.")).toEqual([
      [{ text: "El descanso", em: false }],
      [
        { text: "también", em: true },
        { text: " es un destino.", em: false },
      ],
    ]);
  });

  it("admite énfasis en medio y varios por línea", () => {
    expect(parseRich("Tu próxima\n*pausa* comienza")).toEqual([
      [{ text: "Tu próxima", em: false }],
      [
        { text: "pausa", em: true },
        { text: " comienza", em: false },
      ],
    ]);
    expect(parseRich("a *b* c *d*")).toHaveLength(1);
    expect(parseRich("a *b* c *d*")[0].filter(s => s.em)).toHaveLength(2);
  });

  it("deja literales los asteriscos sin cerrar", () => {
    expect(parseRich("2 * 3")).toEqual([[{ text: "2 * 3", em: false }]]);
  });

  it("acepta CRLF y texto vacío", () => {
    expect(parseRich("a\r\nb")).toHaveLength(2);
    expect(parseRich("")).toEqual([[]]);
  });

  it("no interpreta HTML: el texto queda tal cual", () => {
    expect(parseRich("<b>hola</b>")[0][0].text).toBe("<b>hola</b>");
  });
});

describe("plainRich", () => {
  it("quita marcas y une líneas con espacio", () => {
    expect(plainRich("Todo lo que necesitas.\n*Nada de lo que sobra.*")).toBe(
      "Todo lo que necesitas. Nada de lo que sobra."
    );
  });
});

describe("pad2", () => {
  it("rellena a dos dígitos", () => {
    expect(pad2(2)).toBe("02");
    expect(pad2(12)).toBe("12");
  });
});
