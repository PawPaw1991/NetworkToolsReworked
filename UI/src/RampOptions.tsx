import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import {
  pickRampType,
  rampAngle$,
  rampEntry$,
  rampFlip$,
  rampHeight$,
  rampLength$,
  rampRight$,
  rampTurn$,
  rampType$,
  setRampAngle,
  setRampEntry,
  setRampFlip,
  setRampHeight,
  setRampLength,
  setRampRight,
  setRampTurn,
  useRoadType,
} from "bindings";
import styles from "./ToolPanel.module.scss";
import { Choice, Section, Stepper, indexed } from "./controls";

export const RampOptions = () => {
  const right = useValue(rampRight$);
  const entry = useValue(rampEntry$);
  const flip = useValue(rampFlip$);
  const angle = useValue(rampAngle$);
  const turn = useValue(rampTurn$);
  const length = useValue(rampLength$);
  const height = useValue(rampHeight$);
  const type = useValue(rampType$);

  return (
    <>
      <Choice options={indexed(["Exit", "Entry"])} value={entry ? 1 : 0} onChange={(v) => setRampEntry(v === 1)} />
      <Choice options={indexed(["Left", "Right"])} value={right ? 1 : 0} onChange={(v) => setRampRight(v === 1)} />
      <Choice options={indexed(["Road direction", "Other direction"])} value={flip ? 1 : 0} onChange={(v) => setRampFlip(v === 1)} />
      <Section title="Shape" />
      <Stepper label="Angle" unit="°" value={angle} step={5} fine={1} min={2} max={60} onChange={setRampAngle} />
      <Stepper label="Turn" unit="°" value={turn} step={15} fine={5} min={-180} max={180} onChange={setRampTurn} />
      <Stepper label="Length" unit="m" value={length} step={20} fine={5} min={20} max={800} onChange={setRampLength} />
      <Stepper label="Height" unit="m" value={height} step={1} fine={0.5} min={-40} max={40} onChange={setRampHeight} />
      <Section title="Road type" />
      <div className={styles.row}>
        <span className={styles.label}>{type === "" ? "Same as the road" : type}</span>
        <Button variant="flat" className={styles.choice} onSelect={pickRampType}>Copy from a road</Button>
        {type !== "" && <Button variant="flat" className={styles.choice} onSelect={useRoadType}>Same as road</Button>}
      </div>
    </>
  );
};
