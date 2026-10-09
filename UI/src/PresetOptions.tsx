import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { deletePreset, loadPreset, presetNames$, presetsAvailable$, savePreset } from "bindings";
import styles from "./ToolPanel.module.scss";
import { Section } from "./controls";

/** Saved option sets for the active tool: save the current options, load or delete one. */
export const PresetOptions = () => {
  const available = useValue(presetsAvailable$);
  const names = useValue(presetNames$);
  if (!available) return null;
  const presets = names === "" ? [] : names.split("\n");

  return (
    <>
      <Section title="Presets" />
      {presets.map((name, i) => (
        <div key={name} className={styles.row}>
          <Button variant="flat" className={styles.preset} onSelect={() => loadPreset(i)}>{name}</Button>
          <Button variant="flat" className={styles.step} onSelect={() => deletePreset(i)}>×</Button>
        </div>
      ))}
      <div className={styles.row}>
        <Button variant="flat" className={styles.choice} onSelect={savePreset}>Save current options</Button>
      </div>
    </>
  );
};
