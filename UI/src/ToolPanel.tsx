import classNames from "classnames";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import {
  Phase,
  ToolId,
  activeTool$,
  applyPreview,
  cancelPreview,
  connectMode$,
  connectRotation$,
  panelOpen$,
  parallelHeight$,
  parallelOffset$,
  parallelReverse$,
  phase$,
  rotateConnect,
  selectTool,
  setConnectMode,
  setParallelHeight,
  setParallelOffset,
  setParallelReverse,
  summary$,
  togglePanel,
} from "bindings";
import styles from "./ToolPanel.module.scss";
import { Choice, Stepper, indexed } from "./controls";
import { ShapeOptions } from "./ShapeOptions";
import { SmoothOptions } from "./SmoothOptions";
import { MoveOptions } from "./MoveOptions";
import { ArrangeOptions } from "./ArrangeOptions";

const TOOLS: { id: ToolId; label: string; hint: string }[] = [
  { id: "AddNode", label: "Add Node", hint: "Click a road to split it with a new node." },
  { id: "RemoveNode", label: "Remove Node", hint: "Click a node between two segments of the same road to merge them." },
  { id: "Slope", label: "Slope & Curve", hint: "Re-grade, smooth or straighten the road between two nodes." },
  { id: "Smooth", label: "Smooth", hint: "Smooth out kinks in the road between two nodes. Nodes stay put unless you relax them." },
  { id: "MoveNode", label: "Move Node", hint: "Move a node; the roads attached to it follow." },
  { id: "Arrange", label: "Arrange", hint: "Space the nodes between two nodes evenly, on the current shape, a straight line or an arc." },
  { id: "Reverse", label: "Reverse", hint: "Reverse the direction of the road between two nodes, e.g. a one-way road." },
  { id: "Connect", label: "Connect", hint: "Build a new road between two nodes. , and . rotate the start direction." },
  { id: "Parallel", label: "Parallel", hint: "Build a copy of the road between two nodes, offset to the side." },
];

type Steps = Record<Exclude<Phase, "">, { step: string; text: string }>;

const STEPS: Steps = {
  PickStart: { step: "1/3", text: "Click a start node." },
  PickEnd: { step: "2/3", text: "Hover an end node to preview, click it to lock the preview. Right-click to pick another start." },
  Review: { step: "3/3", text: "Check the preview and adjust the options below. Click or press Apply to build it, right-click or Back to pick another end." },
};

// Tools whose steps read differently from the two-node tools.
const TOOL_STEPS: Partial<Record<ToolId, Steps>> = {
  MoveNode: {
    PickStart: { step: "1/3", text: "Click the node to move." },
    PickEnd: { step: "2/3", text: "Move the cursor to drag it, click to drop. Right-click to pick another node." },
    Review: { step: "3/3", text: "Fine-tune below. Click or press Apply to move it, right-click or Back to drag again." },
  },
};

export const ToolPanel = () => {
  const open = useValue(panelOpen$);
  const active = useValue(activeTool$);
  const connectMode = useValue(connectMode$);
  const offset = useValue(parallelOffset$);
  const height = useValue(parallelHeight$);
  const reverse = useValue(parallelReverse$);
  const phase = useValue(phase$);
  const summary = useValue(summary$);
  const rotation = useValue(connectRotation$);

  if (!open) return null;

  const current = TOOLS.find((t) => t.id === active);
  const steps = TOOL_STEPS[active] ?? STEPS;

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

      {phase !== "" && (
        <div className={styles.status}>
          <div className={styles.row}>
            <span className={styles.stepBadge}>{steps[phase].step}</span>
            <span className={styles.label}>{steps[phase].text}</span>
          </div>
          {summary !== "" && <div className={styles.summary}>{summary}</div>}
          {phase !== "PickStart" && (
            <div className={styles.row}>
              {phase === "Review" && (
                <Button variant="flat" className={classNames(styles.choice, styles.active)} onSelect={applyPreview}>Apply</Button>
              )}
              <Button variant="flat" className={styles.choice} onSelect={cancelPreview}>{phase === "Review" ? "Back" : active === "MoveNode" ? "Pick another" : "Clear start"}</Button>
            </div>
          )}
        </div>
      )}

      {active === "Slope" && <ShapeOptions />}

      {active === "Smooth" && <SmoothOptions />}

      {active === "MoveNode" && <MoveOptions />}

      {active === "Arrange" && <ArrangeOptions />}

      {active === "Connect" && (
        <>
          <Choice options={indexed(["Simple curve", "Smooth both ends"])} value={connectMode} onChange={setConnectMode} />
          <div className={styles.row}>
            <span className={styles.label}>Start direction</span>
            <Button variant="flat" className={styles.step} onSelect={() => rotateConnect(1)}>Left</Button>
            <span className={styles.value}>{rotation}°</span>
            <Button variant="flat" className={styles.step} onSelect={() => rotateConnect(-1)}>Right</Button>
          </div>
        </>
      )}

      {active === "Parallel" && (
        <>
          <Stepper label="Side offset" unit="m" value={offset} step={1} min={-64} max={64} onChange={setParallelOffset} />
          <Stepper label="Height offset" unit="m" value={height} step={1} min={-40} max={40} onChange={setParallelHeight} />
          <Choice options={indexed(["Same direction", "Opposite"])} value={reverse ? 1 : 0} onChange={(v) => setParallelReverse(v === 1)} />
        </>
      )}
    </div>
  );
};
