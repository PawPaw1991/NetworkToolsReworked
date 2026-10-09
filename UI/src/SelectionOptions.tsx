import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { phase$, selectionAvailable$, useSelection, usingSelection$ } from "bindings";
import styles from "./ToolPanel.module.scss";

/** Lets the stretch tools work on the roads selected in Move It instead of two picked nodes. */
export const SelectionRow = () => {
  const available = useValue(selectionAvailable$);
  const using = useValue(usingSelection$);
  const phase = useValue(phase$);
  if (!available || using || (phase !== "PickStart" && phase !== "PickEnd")) return null;
  return (
    <div className={styles.row}>
      <span className={styles.label}>Or work on what Move It has selected</span>
      <Button variant="flat" className={styles.choice} onSelect={useSelection}>Use Move It selection</Button>
    </div>
  );
};
