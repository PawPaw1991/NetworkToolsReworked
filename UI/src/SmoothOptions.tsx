import { useValue } from "cs2/api";
import {
  setSmoothGrades,
  setSmoothKeepEnds,
  setSmoothRelax,
  setSmoothStrength,
  smoothGrades$,
  smoothKeepEnds$,
  smoothRelax$,
  smoothStrength$,
} from "bindings";
import { Choice, Stepper } from "./controls";

export const SmoothOptions = () => {
  const strength = useValue(smoothStrength$);
  const keepEnds = useValue(smoothKeepEnds$);
  const grades = useValue(smoothGrades$);
  const relax = useValue(smoothRelax$);

  return (
    <>
      <Stepper label="Strength" unit="%" value={strength} step={10} fine={1} min={0} max={100} onChange={setSmoothStrength} />
      <Choice
        options={[
          { value: 1, label: "Keep end directions" },
          { value: 0, label: "Free ends" },
        ]}
        value={keepEnds ? 1 : 0}
        onChange={(v) => setSmoothKeepEnds(v === 1)}
      />
      <Choice
        options={[
          { value: 1, label: "Smooth slope too" },
          { value: 0, label: "Curves only" },
        ]}
        value={grades ? 1 : 0}
        onChange={(v) => setSmoothGrades(v === 1)}
      />
      <Stepper label="Relax nodes" unit="%" value={relax} step={10} fine={1} min={0} max={100} onChange={setSmoothRelax} />
    </>
  );
};
