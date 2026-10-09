import { useValue } from "cs2/api";
import { moveHeight$, moveNudgeX$, moveNudgeZ$, setMoveHeight, setMoveNudgeX, setMoveNudgeZ } from "bindings";
import { Stepper } from "./controls";

export const MoveOptions = () => {
  const x = useValue(moveNudgeX$);
  const z = useValue(moveNudgeZ$);
  const height = useValue(moveHeight$);

  return (
    <>
      <Stepper label="Nudge east" unit="m" value={x} step={1} fine={0.1} min={-50} max={50} onChange={setMoveNudgeX} />
      <Stepper label="Nudge north" unit="m" value={z} step={1} fine={0.1} min={-50} max={50} onChange={setMoveNudgeZ} />
      <Stepper label="Height" unit="m" value={height} step={1} fine={0.1} min={-50} max={50} onChange={setMoveHeight} />
    </>
  );
};
