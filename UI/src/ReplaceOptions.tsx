import { useValue } from "cs2/api";
import { replaceKeepUpgrades$, setReplaceKeepUpgrades, setUpgradesSwapSides, upgradesSwapSides$ } from "bindings";
import { Choice, indexed } from "./controls";
import { SourceRow } from "./SourceOptions";

export const ReplaceOptions = () => {
  const keep = useValue(replaceKeepUpgrades$);
  return (
    <>
      <SourceRow label="New type" />
      <Choice options={indexed(["Keep upgrades", "Drop upgrades"])} value={keep ? 0 : 1} onChange={(v) => setReplaceKeepUpgrades(v === 0)} />
    </>
  );
};

export const UpgradesOptions = () => {
  const swap = useValue(upgradesSwapSides$);
  return (
    <>
      <SourceRow label="Copying" />
      <Choice options={indexed(["Same sides", "Swap left and right"])} value={swap ? 1 : 0} onChange={(v) => setUpgradesSwapSides(v === 1)} />
    </>
  );
};
