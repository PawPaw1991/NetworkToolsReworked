import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { pickSource, sourceName$ } from "bindings";
import styles from "./ToolPanel.module.scss";

/** The road a tool copies from (type or upgrades), with a button to pick another. */
export const SourceRow = ({ label }: { label: string }) => {
  const source = useValue(sourceName$);
  if (source === "") return null;
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <span className={styles.value}>{source}</span>
      <Button variant="flat" className={styles.choice} onSelect={pickSource}>Pick another</Button>
    </div>
  );
};
