import { useValue } from "cs2/api";
import {
  moveGridSize$,
  moveHeight$,
  moveNudgeX$,
  moveNudgeZ$,
  moveSnap$,
  setMoveGridSize,
  setMoveHeight,
  setMoveNudgeX,
  setMoveNudgeZ,
  setMoveSnap,
} from "bindings";
import { Choice, Stepper, indexed } from "./controls";

export const MoveOptions = () => {
  const x = useValue(moveNudgeX$);
  const z = useValue(moveNudgeZ$);
  const height = useValue(moveHeight$);
  const snap = useValue(moveSnap$);
  const grid = useValue(moveGridSize$);

  return (
    <>
      <Choice options={indexed(["No snap", "Grid", "15° steps"])} value={snap} onChange={setMoveSnap} />
      {snap === 1 && <Stepper label="Grid size" unit="m" value={grid} step={1} fine={0.5} min={0.5} max={32} onChange={setMoveGridSize} />}
      <Stepper label="Nudge east" unit="m" value={x} step={1} fine={0.1} min={-50} max={50} onChange={setMoveNudgeX} />
      <Stepper label="Nudge north" unit="m" value={z} step={1} fine={0.1} min={-50} max={50} onChange={setMoveNudgeZ} />
      <Stepper label="Height" unit="m" value={height} step={1} fine={0.1} min={-50} max={50} onChange={setMoveHeight} />
    </>
  );
};
