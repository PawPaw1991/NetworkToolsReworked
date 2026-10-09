import { useValue } from "cs2/api";
import {
  roundaboutClockwise$,
  roundaboutCustomRing$,
  roundaboutRadius$,
  setRoundaboutClockwise,
  setRoundaboutCustomRing,
  setRoundaboutRadius,
} from "bindings";
import { Choice, Stepper, indexed } from "./controls";
import { RoadTypePicker } from "./RoadTypePicker";

export const RoundaboutOptions = () => {
  const radius = useValue(roundaboutRadius$);
  const clockwise = useValue(roundaboutClockwise$);
  const customRing = useValue(roundaboutCustomRing$);

  return (
    <>
      <Choice options={indexed(["Game roundabout", "Custom ring"])} value={customRing ? 1 : 0} onChange={(v) => setRoundaboutCustomRing(v === 1)} />
      {customRing && (
        <>
          <Stepper label="Radius" unit="m" value={radius} step={4} fine={0.5} min={8} max={200} onChange={setRoundaboutRadius} />
          <Choice options={indexed(["Anticlockwise", "Clockwise"])} value={clockwise ? 1 : 0} onChange={(v) => setRoundaboutClockwise(v === 1)} />
          <RoadTypePicker />
        </>
      )}
    </>
  );
};
