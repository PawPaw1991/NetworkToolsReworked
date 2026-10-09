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
import { RoundaboutOptions } from "./RoundaboutOptions";
import { ReplaceOptions, UpgradesOptions } from "./ReplaceOptions";

const TOOLS: { id: ToolId; label: string; hint: string }[] = [
  { id: "AddNode", label: "Add Node", hint: "Click a road to split it with a new node." },
  { id: "RemoveNode", label: "Remove Node", hint: "Click a node between two segments of the same road to merge them." },
  { id: "Slope", label: "Slope & Curve", hint: "Re-grade, smooth or straighten the road between two nodes." },
  { id: "Smooth", label: "Smooth", hint: "Smooth out kinks in the road between two nodes. Nodes stay put unless you relax them." },
  { id: "MoveNode", label: "Move Node", hint: "Move a node; the roads attached to it follow." },
  { id: "Arrange", label: "Arrange", hint: "Space the nodes between two nodes evenly, on the current shape, a straight line or an arc." },
  { id: "Reverse", label: "Reverse", hint: "Reverse the direction of the road between two nodes, e.g. a one-way road." },
  { id: "Roundabout", label: "Roundabout", hint: "Turn a junction into a roundabout. The ring uses the same road type as the junction." },
  { id: "Replace", label: "Change type", hint: "Turn the road between two nodes into another road type, keeping its shape and height." },
  { id: "Upgrades", label: "Copy upgrades", hint: "Give the road between two nodes the same upgrades (trees, sidewalks, walls...) as another road." },
  { id: "Intersect", label: "Intersect", hint: "Join two roads that cross without a junction. Hover near the crossing." },
  { id: "Undo", label: "Undo", hint: "Undo the last edit made with these tools (last 30 this session). Ctrl+Alt+Z." },
  { id: "Connect", label: "Connect", hint: "Build a new road between two nodes. , and . rotate the start direction." },
  { id: "Parallel", label: "Parallel", hint: "Build a copy of the road between two nodes, offset to the side." },
];

type Step = { step: string; text: string };
type Steps = Record<"PickStart" | "PickEnd" | "Review", Step> & { PickSource?: Step };

const STEPS: Steps = {
  PickStart: { step: "1/3", text: "Click a start node." },
  PickEnd: { step: "2/3", text: "Hover an end node to preview, click it to lock the preview. Right-click to pick another start." },
  Review: { step: "3/3", text: "Check the preview and adjust the options below. Click or press Apply to build it, right-click or Back to pick another end." },
};

// Tools whose steps read differently from the two-node tools.
const UNDO_STEP = { step: "", text: "Red roads are removed and green ones restored. Click or press Apply to undo, right-click or Cancel to keep things as they are." };

const TOOL_STEPS: Partial<Record<ToolId, Steps>> = {
  Upgrades: {
    PickSource: { step: "1/4", text: "Click a road whose upgrades you want to copy (one without upgrades clears them)." },
    PickStart: { step: "2/4", text: "Click a start node. Right-click to copy another road." },
    PickEnd: { step: "3/4", text: "Hover an end node to preview, click it to lock the preview. Right-click to pick another start." },
    Review: { step: "4/4", text: "Click or press Apply to change the upgrades, right-click or Back to pick another end." },
  },
  Replace: {
    PickSource: { step: "1/4", text: "Click a road of the type you want to use." },
    PickStart: { step: "2/4", text: "Click a start node. Right-click to copy another type." },
    PickEnd: { step: "3/4", text: "Hover an end node to preview, click it to lock the preview. Right-click to pick another start." },
    Review: { step: "4/4", text: "Click or press Apply to change the type, right-click or Back to pick another end." },
  },
  Undo: { PickStart: UNDO_STEP, PickEnd: UNDO_STEP, Review: UNDO_STEP },
  Intersect: {
    PickStart: { step: "1/2", text: "Hover a road near where another crosses it, click to lock the preview." },
    PickEnd: { step: "1/2", text: "Hover a road near where another crosses it, click to lock the preview." },
    Review: { step: "2/2", text: "Click or press Apply to make the junction, right-click or Back to pick another crossing." },
  },
  Roundabout: {
    PickStart: { step: "1/2", text: "Hover a junction to preview, click it to lock the preview." },
    PickEnd: { step: "1/2", text: "Hover a junction to preview, click it to lock the preview." },
    Review: { step: "2/2", text: "Set the radius and direction below. Click or press Apply to build it, right-click or Back to pick another junction." },
  },
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
  const step = phase === "" ? undefined : steps[phase];

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

      {step && (
        <div className={styles.status}>
          <div className={styles.row}>
            {step.step !== "" && <span className={styles.stepBadge}>{step.step}</span>}
            <span className={styles.label}>{step.text}</span>
          </div>
          {summary !== "" && <div className={styles.summary}>{summary}</div>}
          {phase !== "PickStart" && phase !== "PickSource" && (
            <div className={styles.row}>
              {phase === "Review" && (
                <Button variant="flat" className={classNames(styles.choice, styles.active)} onSelect={applyPreview}>Apply</Button>
              )}
              <Button variant="flat" className={styles.choice} onSelect={cancelPreview}>{active === "Undo" ? "Cancel" : phase === "Review" ? "Back" : active === "MoveNode" ? "Pick another" : "Clear start"}</Button>
            </div>
          )}
        </div>
      )}

      {active === "Slope" && <ShapeOptions />}

      {active === "Smooth" && <SmoothOptions />}

      {active === "MoveNode" && <MoveOptions />}

      {active === "Arrange" && <ArrangeOptions />}

      {active === "Roundabout" && <RoundaboutOptions />}

      {active === "Replace" && <ReplaceOptions />}

      {active === "Upgrades" && <UpgradesOptions />}

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
