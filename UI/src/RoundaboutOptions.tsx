import { useValue } from "cs2/api";
import { roundaboutClockwise$, roundaboutRadius$, setRoundaboutClockwise, setRoundaboutRadius } from "bindings";
import { Choice, Stepper } from "./controls";

export const RoundaboutOptions = () => {
  const radius = useValue(roundaboutRadius$);
  const clockwise = useValue(roundaboutClockwise$);

  return (
    <>
      <Stepper label="Radius" unit="m" value={radius} step={4} fine={0.5} min={8} max={200} onChange={setRoundaboutRadius} />
      <Choice
        options={[
          { value: 0, label: "Anticlockwise" },
          { value: 1, label: "Clockwise" },
        ]}
        value={clockwise ? 1 : 0}
        onChange={(v) => setRoundaboutClockwise(v === 1)}
      />
    </>
  );
};
