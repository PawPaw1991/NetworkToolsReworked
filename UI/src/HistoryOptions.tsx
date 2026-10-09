import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { rollBack, undoHistory$ } from "bindings";
import styles from "./ToolPanel.module.scss";
import { Section } from "./controls";

/** The edits that can be undone, newest first. Picking one undoes it and everything after it. */
export const HistoryOptions = () => {
  const text = useValue(undoHistory$);
  const steps = text === "" ? [] : text.split("\n");
  if (steps.length === 0) return null;
  return (
    <>
      <Section title="History (newest first)" />
      <div className={styles.issues}>
        {steps.map((label, i) => (
          <Button key={i} variant="flat" className={styles.issue} onSelect={() => rollBack(i + 1)}>
            <span className={styles.fixable}>{i === 0 ? "Undo" : `Undo ${i + 1}`}</span> {label}
          </Button>
        ))}
      </div>
      <div className={styles.summary}>Picking an edit undoes it and every edit after it, newest first.</div>
    </>
  );
};
