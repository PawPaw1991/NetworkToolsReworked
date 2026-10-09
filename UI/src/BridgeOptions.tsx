import { useValue } from "cs2/api";
import { bridgeClearance$, bridgeHeight$, bridgeMode$, setBridgeClearance, setBridgeHeight, setBridgeMode } from "bindings";
import { Choice, indexed, Stepper } from "./controls";

export const BridgeOptions = () => {
  const mode = useValue(bridgeMode$);
  const height = useValue(bridgeHeight$);
  const clearance = useValue(bridgeClearance$);
  return (
    <>
      <Choice options={indexed(["Over crossings", "Raise (bridge)", "Lower (tunnel)"])} value={mode} onChange={setBridgeMode} />
      {mode === 0 ? (
        <Stepper label="Clearance above crossing roads" unit="m" value={clearance} step={1} fine={0.5} min={4} max={30} onChange={setBridgeClearance} />
      ) : (
        <Stepper label={mode === 1 ? "Raise by" : "Lower by"} unit="m" value={height} step={1} fine={0.5} min={1} max={60} onChange={setBridgeHeight} />
      )}
    </>
  );
};
