import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import {
  deleteLayout,
  duplicateAngle$,
  duplicateHasGroup$,
  duplicateMode$,
  duplicateUseSelection,
  layouts$,
  loadLayout,
  moveItAvailable$,
  phase$,
  saveLayout,
  setDuplicateAngle,
  setDuplicateMode,
} from "bindings";
import styles from "./ToolPanel.module.scss";
import { Choice, Section, Stepper, indexed } from "./controls";

export const DuplicateOptions = () => {
  const mode = useValue(duplicateMode$);
  const angle = useValue(duplicateAngle$);
  const hasGroup = useValue(duplicateHasGroup$);
  const moveIt = useValue(moveItAvailable$);
  const phase = useValue(phase$);
  const names = useValue(layouts$);
  const layouts = names === "" ? [] : names.split("\n");
  const picking = phase === "PickStart" || phase === "PickEnd";

  return (
    <>
      <Choice options={indexed(["Copy", "Mirror"])} value={mode} onChange={setDuplicateMode} />
      <Stepper label={mode === 0 ? "Turn" : "Mirror axis"} unit="°" value={angle} step={15} fine={1} min={-180} max={180} onChange={setDuplicateAngle} />
      {picking && moveIt && (
        <div className={styles.row}>
          <Button variant="flat" className={styles.choice} onSelect={duplicateUseSelection}>Copy Move It selection</Button>
        </div>
      )}
      {hasGroup && (
        <div className={styles.row}>
          <Button variant="flat" className={styles.choice} onSelect={saveLayout}>Save as layout</Button>
        </div>
      )}
      {layouts.length > 0 && <Section title="Saved layouts" />}
      {layouts.map((name, i) => (
        <div key={`${i}-${name}`} className={styles.row}>
          <Button variant="flat" className={styles.preset} onSelect={() => loadLayout(i)}>{name}</Button>
          <Button variant="flat" className={styles.step} onSelect={() => deleteLayout(i)}>×</Button>
        </div>
      ))}
    </>
  );
};
