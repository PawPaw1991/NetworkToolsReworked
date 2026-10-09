import { useValue } from "cs2/api";
import {
  parallelBothSides$,
  parallelGap$,
  parallelHeight$,
  parallelOffset$,
  parallelReverse$,
  parallelSpacing$,
  parallelTaper$,
  parallelWidths$,
  setParallelBothSides,
  setParallelGap,
  setParallelHeight,
  setParallelOffset,
  setParallelReverse,
  setParallelSpacing,
  setParallelTaper,
  setParallelWidths,
} from "bindings";
import { Choice, Section, Stepper, indexed } from "./controls";

export const ParallelOptions = () => {
  const offset = useValue(parallelOffset$);
  const height = useValue(parallelHeight$);
  const reverse = useValue(parallelReverse$);
  const spacing = useValue(parallelSpacing$);
  const gap = useValue(parallelGap$);
  const widths = useValue(parallelWidths$);
  const bothSides = useValue(parallelBothSides$);
  const taper = useValue(parallelTaper$);
  const right = offset >= 0;

  return (
    <>
      <Section title="Spacing" />
      <Choice options={indexed(["Metres", "Touching", "Road widths"])} value={spacing} onChange={setParallelSpacing} />
      {spacing === 0 && <Stepper label="Side offset" unit="m" value={offset} step={1} fine={0.5} min={-64} max={64} onChange={setParallelOffset} />}
      {spacing === 1 && <Stepper label="Gap" unit="m" value={gap} step={1} fine={0.1} min={0} max={32} onChange={setParallelGap} />}
      {spacing === 2 && <Stepper label="Widths" unit="×" value={widths} step={1} min={1} max={8} onChange={setParallelWidths} />}
      <Choice
        options={indexed(["Left", "Right", "Both sides"])}
        value={bothSides ? 2 : right ? 1 : 0}
        onChange={(v) => {
          setParallelBothSides(v === 2);
          if (v !== 2 && (v === 1) !== right) setParallelOffset(offset === 0 ? (v === 1 ? 16 : -16) : -offset);
        }}
      />
      <Section title="Taper" />
      <Choice options={indexed(["None", "Split off at start", "Merge in at end"])} value={taper} onChange={setParallelTaper} />
      <Section title="Height and direction" />
      <Stepper label="Height offset" unit="m" value={height} step={1} fine={0.5} min={-40} max={40} onChange={setParallelHeight} />
      <Choice options={indexed(["Same direction", "Opposite"])} value={reverse ? 1 : 0} onChange={(v) => setParallelReverse(v === 1)} />
    </>
  );
};
