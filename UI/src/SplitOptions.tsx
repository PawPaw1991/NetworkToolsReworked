import { useValue } from "cs2/api";
import {
  setSimplifyTolerance,
  setSplitMode,
  setSplitParts,
  setSplitSpacing,
  simplifyTolerance$,
  splitMode$,
  splitParts$,
  splitSpacing$,
} from "bindings";
import { Choice, indexed, Stepper } from "./controls";

export const SplitOptions = () => {
  const mode = useValue(splitMode$);
  const parts = useValue(splitParts$);
  const spacing = useValue(splitSpacing$);
  const tolerance = useValue(simplifyTolerance$);
  return (
    <>
      <Choice options={indexed(["Equal parts", "Every", "Simplify"])} value={mode} onChange={setSplitMode} />
      {mode === 0 && <Stepper label="Parts per segment" unit="" value={parts} step={1} min={2} max={16} onChange={setSplitParts} />}
      {mode === 1 && <Stepper label="Part length" unit="m" value={spacing} step={8} fine={1} min={8} max={400} onChange={setSplitSpacing} />}
      {mode === 2 && <Stepper label="Tolerance" unit="m" value={tolerance} step={0.5} fine={0.1} min={0.1} max={5} onChange={setSimplifyTolerance} />}
    </>
  );
};
