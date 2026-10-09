import { useValue } from "cs2/api";
import { matchHeight$, phase$, setMatchHeight } from "bindings";
import { Stepper } from "./controls";

export const MatchHeightOptions = () => {
  const height = useValue(matchHeight$);
  const phase = useValue(phase$);
  if (phase !== "PickEnd") return null;
  return <Stepper label="Target height" unit="m" value={height} step={1} fine={0.1} min={-1000} max={5000} onChange={setMatchHeight} />;
};
