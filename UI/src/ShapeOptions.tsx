import { useValue } from "cs2/api";
import {
  curveKeepEnds$,
  curveMode$,
  curveStrength$,
  setCurveKeepEnds,
  setCurveMode,
  setCurveStrength,
  setSlopeArch,
  setSlopeEase,
  setSlopeEndOffset,
  setSlopeProfile,
  setSlopeStartOffset,
  slopeArch$,
  slopeEase$,
  slopeEndOffset$,
  slopeProfile$,
  slopeStartOffset$,
} from "bindings";
import { Choice, Section, Stepper } from "./controls";

// Indices match the C# enums SlopeProfile (Linear, EaseInOut, Keep) and CurveMode (Keep, Smooth, Straighten).
const PROFILES = [
  { value: 2, label: "Keep" },
  { value: 0, label: "Linear" },
  { value: 1, label: "Ease in/out" },
];
const CURVES = [
  { value: 0, label: "Keep" },
  { value: 1, label: "Smooth" },
  { value: 2, label: "Straighten" },
];

export const ShapeOptions = () => {
  const profile = useValue(slopeProfile$);
  const ease = useValue(slopeEase$);
  const arch = useValue(slopeArch$);
  const startOffset = useValue(slopeStartOffset$);
  const endOffset = useValue(slopeEndOffset$);
  const curve = useValue(curveMode$);
  const strength = useValue(curveStrength$);
  const keepEnds = useValue(curveKeepEnds$);

  return (
    <>
      <Section title="Slope" />
      <Choice options={PROFILES} value={profile} onChange={setSlopeProfile} />
      {profile === 1 && <Stepper label="Ease length" unit="%" value={ease} step={5} min={5} max={50} onChange={setSlopeEase} />}
      <Stepper label="Arch" unit="m" value={arch} step={1} fine={0.1} min={-30} max={30} onChange={setSlopeArch} />
      <Stepper label="Start height" unit="m" value={startOffset} step={1} fine={0.1} min={-50} max={50} onChange={setSlopeStartOffset} />
      <Stepper label="End height" unit="m" value={endOffset} step={1} fine={0.1} min={-50} max={50} onChange={setSlopeEndOffset} />

      <Section title="Curve" />
      <Choice options={CURVES} value={curve} onChange={setCurveMode} />
      {curve !== 0 && <Stepper label="Strength" unit="%" value={strength} step={10} fine={1} min={0} max={100} onChange={setCurveStrength} />}
      {curve === 1 && (
        <Choice
          options={[
            { value: 1, label: "Keep end directions" },
            { value: 0, label: "Free ends" },
          ]}
          value={keepEnds ? 1 : 0}
          onChange={(v) => setCurveKeepEnds(v === 1)}
        />
      )}
    </>
  );
};
