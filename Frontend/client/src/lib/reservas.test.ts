import { describe, expect, it } from "vitest";
import {
  aIso,
  deIso,
  nocheOcupada,
  nochesEntre,
  rangoLibre,
  salidaMaxima,
  sumarDias,
} from "./reservas";

const ocupado = [
  { desde: "2027-06-08", hasta: "2027-06-13" },
  { desde: "2027-06-20", hasta: "2027-06-22" },
];

describe("fechas iso", () => {
  it("ida y vuelta sin correrse de día", () => {
    expect(aIso(deIso("2027-01-31"))).toBe("2027-01-31");
    expect(sumarDias("2027-02-27", 2)).toBe("2027-03-01");
    expect(sumarDias("2027-01-01", -1)).toBe("2026-12-31");
  });

  it("cuenta noches", () => {
    expect(nochesEntre("2027-06-08", "2027-06-13")).toBe(5);
    expect(nochesEntre("2027-03-13", "2027-03-15")).toBe(2);
  });
});

describe("ocupación", () => {
  it("la noche de salida queda libre", () => {
    expect(nocheOcupada(ocupado, "2027-06-12")).toBe(true);
    expect(nocheOcupada(ocupado, "2027-06-13")).toBe(false);
    expect(nocheOcupada(ocupado, "2027-06-07")).toBe(false);
  });

  it("salida máxima = inicio del siguiente tramo ocupado", () => {
    expect(salidaMaxima(ocupado, "2027-06-14")).toBe("2027-06-20");
    expect(salidaMaxima(ocupado, "2027-06-01")).toBe("2027-06-08");
    expect(salidaMaxima(ocupado, "2027-06-25")).toBeNull();
  });

  it("rango libre permite encadenar salida y llegada", () => {
    expect(rangoLibre(ocupado, "2027-06-13", "2027-06-20")).toBe(true);
    expect(rangoLibre(ocupado, "2027-06-05", "2027-06-08")).toBe(true);
    expect(rangoLibre(ocupado, "2027-06-05", "2027-06-09")).toBe(false);
    expect(rangoLibre(ocupado, "2027-06-14", "2027-06-21")).toBe(false);
  });
});
