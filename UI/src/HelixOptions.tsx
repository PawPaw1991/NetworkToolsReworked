import { useValue } from "cs2/api";
import {
  helixClimb$,
  helixClockwise$,
  helixRadius$,
  helixStartAngle$,
  helixStartHeight$,
  helixTurns$,
  pickHelixType,
  setHelixClimb,
  setHelixClockwise,
  setHelixRadius,
  setHelixStartAngle,
  setHelixStartHeight,
  setHelixTurns,
} from "bindings";
import { Choice, Section, Stepper, indexed } from "./controls";
import { RoadTypePicker } from "./RoadTypePicker";

export const HelixOptions = () => {
  const radius = useValue(helixRadius$);
  const turns = useValue(helixTurns$);
  const climb = useValue(helixClimb$);
  const clockwise = useValue(helixClockwise$);
  const startAngle = useValue(helixStartAngle$);
  const startHeight = useValue(helixStartHeight$);

  return (
    <>
      <Choice options={indexed(["Anticlockwise", "Clockwise"])} value={clockwise ? 1 : 0} onChange={(v) => setHelixClockwise(v === 1)} />
      <Stepper label="Radius" unit="m" value={radius} step={5} fine={1} min={8} max={500} onChange={setHelixRadius} />
      <Stepper label="Turns" unit="" value={turns} step={1} fine={0.25} min={0.25} max={12} onChange={setHelixTurns} />
      <Stepper label="Climb per turn" unit="m" value={climb} step={1} fine={0.5} min={-60} max={60} onChange={setHelixClimb} />
      <Section title="Placed on the ground" />
      <Stepper label="Start angle" unit="°" value={startAngle} step={45} fine={5} min={-180} max={180} onChange={setHelixStartAngle} />
      <Stepper label="Start height" unit="m" value={startHeight} step={1} fine={0.5} min={-40} max={60} onChange={setHelixStartHeight} />
      <RoadTypePicker onCopyFromRoad={pickHelixType} />
    </>
  );
};
