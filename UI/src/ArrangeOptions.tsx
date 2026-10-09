import { useValue } from "cs2/api";
import { arrangeBulge$, arrangeMode$, setArrangeBulge, setArrangeMode } from "bindings";
import { Choice, Stepper, indexed } from "./controls";

// Indices match the C# enum ArrangeMode (EvenSpacing, Line, Arc).
export const ArrangeOptions = () => {
  const mode = useValue(arrangeMode$);
  const bulge = useValue(arrangeBulge$);

  return (
    <>
      <Choice options={indexed(["Even spacing", "Straight line", "Arc"])} value={mode} onChange={setArrangeMode} />
      {mode === 2 && <Stepper label="Bulge" unit="%" value={bulge} step={10} fine={1} min={-300} max={300} onChange={setArrangeBulge} />}
    </>
  );
};
