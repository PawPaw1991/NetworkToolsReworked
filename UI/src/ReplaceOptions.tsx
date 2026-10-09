import { useValue } from "cs2/api";
import { replaceKeepUpgrades$, setReplaceKeepUpgrades } from "bindings";
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
