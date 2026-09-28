import { Fragment } from "react";
import { parseRich } from "@/lib/rich";

/** Renderiza texto con "\n" y "*énfasis*" como <br/> y <em>, sin dangerouslySetInnerHTML. */
export function RichTitle({ text }: { text: string }) {
  return (
    <>
      {parseRich(text).map((linea, i) => (
        <Fragment key={i}>
          {i > 0 && <br />}
          {linea.map((s, j) => (s.em ? <em key={j}>{s.text}</em> : <Fragment key={j}>{s.text}</Fragment>))}
        </Fragment>
      ))}
    </>
  );
}
