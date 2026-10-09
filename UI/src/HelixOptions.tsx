import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import {
  helixClimb$,
  helixClockwise$,
  helixRadius$,
  helixStartAngle$,
  helixStartHeight$,
  helixTurns$,
  helixType$,
  helixUseRoadType,
  pickHelixType,
  setHelixClimb,
  setHelixClockwise,
  setHelixRadius,
  setHelixStartAngle,
  setHelixStartHeight,
  setHelixTurns,
} from "bindings";
import styles from "./ToolPanel.module.scss";
import { Choice, Section, Stepper, indexed } from "./controls";

export const HelixOptions = () => {
  const radius = useValue(helixRadius$);
  const turns = useValue(helixTurns$);
  const climb = useValue(helixClimb$);
  const clockwise = useValue(helixClockwise$);
  const startAngle = useValue(helixStartAngle$);
  const startHeight = useValue(helixStartHeight$);
  const type = useValue(helixType$);

  return (
    <>
      <Choice options={indexed(["Anticlockwise", "Clockwise"])} value={clockwise ? 1 : 0} onChange={(v) => setHelixClockwise(v === 1)} />
      <Stepper label="Radius" unit="m" value={radius} step={5} fine={1} min={8} max={500} onChange={setHelixRadius} />
      <Stepper label="Turns" unit="" value={turns} step={1} fine={0.25} min={0.25} max={12} onChange={setHelixTurns} />
      <Stepper label="Climb per turn" unit="m" value={climb} step={1} fine={0.5} min={-60} max={60} onChange={setHelixClimb} />
      <Section title="Placed on the ground" />
      <Stepper label="Start angle" unit="°" value={startAngle} step={45} fine={5} min={-180} max={180} onChange={setHelixStartAngle} />
      <Stepper label="Start height" unit="m" value={startHeight} step={1} fine={0.5} min={-40} max={60} onChange={setHelixStartHeight} />
      <Section title="Road type" />
      <div className={styles.row}>
        <span className={styles.label}>{type === "" ? "Same as the road" : type}</span>
        <Button variant="flat" className={styles.choice} onSelect={pickHelixType}>Copy from a road</Button>
        {type !== "" && <Button variant="flat" className={styles.choice} onSelect={helixUseRoadType}>Same as road</Button>}
      </div>
    </>
  );
};
