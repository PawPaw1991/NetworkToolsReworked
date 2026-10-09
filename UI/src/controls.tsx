import classNames from "classnames";
import { Button } from "cs2/ui";
import styles from "./ToolPanel.module.scss";

export type Option = { value: number; label: string };

export const Choice = ({ options, value, onChange }: { options: Option[]; value: number; onChange: (v: number) => void }) => (
  <div className={styles.row}>
    {options.map((o) => (
      <Button key={o.label} variant="flat" className={classNames(styles.choice, { [styles.active]: value === o.value })} onSelect={() => onChange(o.value)}>
        {o.label}
      </Button>
    ))}
  </div>
);

/** Plain numbered options 0..n-1. */
export const indexed = (labels: string[]): Option[] => labels.map((label, value) => ({ value, label }));

export const Section = ({ title }: { title: string }) => <div className={styles.section}>{title}</div>;

const round = (v: number) => Math.round(v * 100) / 100;

// As many decimals as the fine step has (0.5 shows one, 0.25 two).
const format = (v: number, fine?: number) =>
  fine !== undefined && fine < 1 ? v.toFixed(Math.min(2, (String(fine).split(".")[1] ?? "0").length)) : String(Math.round(v));

/** Number with coarse (−/+) and optional fine (‹/›) steps. */
export const Stepper = ({
  label,
  unit,
  value,
  step,
  fine,
  min,
  max,
  onChange,
}: {
  label: string;
  unit: string;
  value: number;
  step: number;
  fine?: number;
  min: number;
  max: number;
  onChange: (v: number) => void;
}) => {
  const set = (v: number) => onChange(round(Math.min(max, Math.max(min, v))));
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <Button variant="flat" className={styles.step} onSelect={() => set(value - step)}>−</Button>
      {fine !== undefined && <Button variant="flat" className={styles.step} onSelect={() => set(value - fine)}>‹</Button>}
      <span className={styles.value}>
        {format(value, fine)} {unit}
      </span>
      {fine !== undefined && <Button variant="flat" className={styles.step} onSelect={() => set(value + fine)}>›</Button>}
      <Button variant="flat" className={styles.step} onSelect={() => set(value + step)}>+</Button>
    </div>
  );
};
