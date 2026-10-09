import { useValue } from "cs2/api";
import { filletRadius$, setFilletRadius } from "bindings";
import { Stepper } from "./controls";

export const FilletOptions = () => {
  const radius = useValue(filletRadius$);
  return <Stepper label="Radius" unit="m" value={radius} step={10} fine={1} min={5} max={1000} onChange={setFilletRadius} />;
};
