import classNames from "classnames";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { healthIssues$, healthMinLength$, healthSelected$, rescanHealth, selectIssue, setHealthMinLength } from "bindings";
import styles from "./ToolPanel.module.scss";
import { Section, Stepper } from "./controls";

/** The network check's findings: click one to jump to it (and preview its fix, if it has one). */
export const HealthOptions = () => {
  const text = useValue(healthIssues$);
  const selected = useValue(healthSelected$);
  const minLength = useValue(healthMinLength$);
  const issues = text === "" ? [] : text.split("\n").map((line) => line.split("\t"));

  return (
    <>
      <Stepper label="Report segments shorter than" unit="m" value={minLength} step={1} fine={0.5} min={0} max={20} onChange={setHealthMinLength} />
      <div className={styles.row}>
        <Button variant="flat" className={styles.choice} onSelect={rescanHealth}>Scan again</Button>
      </div>
      {issues.length > 0 && <Section title="Found" />}
      <div className={styles.issues}>
        {issues.map(([fix, kind, description], i) => (
          <Button
            key={i}
            variant="flat"
            className={classNames(styles.issue, { [styles.active]: selected === i })}
            onSelect={() => selectIssue(i)}
          >
            <span className={fix === "fix" ? styles.fixable : styles.report}>{kind}</span> {description}
          </Button>
        ))}
      </div>
    </>
  );
};
