import classNames from "classnames";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import {
  ToolId,
  activeTool$,
  connectMode$,
  panelOpen$,
  parallelHeight$,
  parallelOffset$,
  parallelReverse$,
  selectTool,
  setConnectMode,
  setParallelHeight,
  setParallelOffset,
  setParallelReverse,
  setSlopeProfile,
  slopeProfile$,
  togglePanel,
} from "bindings";
import styles from "./ToolPanel.module.scss";

const TOOLS: { id: ToolId; label: string; hint: string }[] = [
  { id: "AddNode", label: "Add Node", hint: "Click a road to split it with a new node." },
  { id: "RemoveNode", label: "Remove Node", hint: "Click a node between two segments of the same road to merge them." },
  { id: "Slope", label: "Slope", hint: "Click a start node, hover an end node to preview, click to re-grade the road between them." },
  { id: "Connect", label: "Connect", hint: "Click a start node, hover an end node to preview a new road, click to build it. , and . rotate the start direction." },
  { id: "Parallel", label: "Parallel", hint: "Click a start node, hover an end node to preview a copy of the road, click to build it." },
];

const Choice = ({ options, value, onChange }: { options: string[]; value: number; onChange: (v: number) => void }) => (
  <div className={styles.row}>
    {options.map((label, i) => (
      <Button key={label} variant="flat" className={classNames(styles.choice, { [styles.active]: value === i })} onSelect={() => onChange(i)}>
        {label}
      </Button>
    ))}
  </div>
);

const Stepper = ({ label, value, step, min, max, onChange }: { label: string; value: number; step: number; min: number; max: number; onChange: (v: number) => void }) => {
  const set = (v: number) => onChange(Math.min(max, Math.max(min, v)));
  return (
    <div className={styles.row}>
      <span className={styles.label}>{label}</span>
      <Button variant="flat" className={styles.step} onSelect={() => set(value - step)}>−</Button>
      <span className={styles.value}>{value} m</span>
      <Button variant="flat" className={styles.step} onSelect={() => set(value + step)}>+</Button>
    </div>
  );
};

export const ToolPanel = () => {
  const open = useValue(panelOpen$);
  const active = useValue(activeTool$);
  const slopeProfile = useValue(slopeProfile$);
  const connectMode = useValue(connectMode$);
  const offset = useValue(parallelOffset$);
  const height = useValue(parallelHeight$);
  const reverse = useValue(parallelReverse$);

  if (!open) return null;

  const current = TOOLS.find((t) => t.id === active);

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <span>Network Tools</span>
        <Button variant="flat" className={styles.close} onSelect={togglePanel}>×</Button>
      </div>

      <div className={styles.tools}>
        {TOOLS.map((tool) => (
          <Button
            key={tool.id}
            variant="flat"
            className={classNames(styles.tool, { [styles.active]: active === tool.id })}
            onSelect={() => selectTool(active === tool.id ? "None" : tool.id)}
          >
            {tool.label}
          </Button>
        ))}
      </div>

      {current && <div className={styles.hint}>{current.hint}</div>}

      {active === "Slope" && <Choice options={["Linear", "Ease in/out"]} value={slopeProfile} onChange={setSlopeProfile} />}

      {active === "Connect" && <Choice options={["Simple curve", "Smooth both ends"]} value={connectMode} onChange={setConnectMode} />}

      {active === "Parallel" && (
        <>
          <Stepper label="Side offset" value={offset} step={1} min={-64} max={64} onChange={setParallelOffset} />
          <Stepper label="Height offset" value={height} step={1} min={-40} max={40} onChange={setParallelHeight} />
          <Choice options={["Same direction", "Opposite"]} value={reverse ? 1 : 0} onChange={(v) => setParallelReverse(v === 1)} />
        </>
      )}
    </div>
  );
};
