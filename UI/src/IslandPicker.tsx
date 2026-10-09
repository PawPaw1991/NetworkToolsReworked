import classNames from "classnames";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button } from "cs2/ui";
import { roundaboutIsland$, roundaboutIslands$, setRoundaboutIsland } from "bindings";
import styles from "./ToolPanel.module.scss";
import { Section } from "./controls";

/** Chooses which of the game's roundabout central islands the Roundabout tool places. */
export const IslandPicker = () => {
  const raw = useValue(roundaboutIslands$);
  const current = useValue(roundaboutIsland$);
  const { translate } = useLocalization();

  const islands = raw
    .split("\n")
    .filter((line) => line !== "")
    .map((line) => {
      const [name, icon] = line.split("\t");
      return { name, icon: icon ?? "", label: translate(`Assets.NAME[${name}]`, name) ?? name };
    });

  return (
    <>
      <Section title="Island" />
      <div className={styles.typeList}>
        {islands.length === 0 && <div className={styles.hint}>No roundabout islands loaded.</div>}
        {islands.map((island) => (
          <Button
            key={island.name}
            variant="flat"
            className={classNames(styles.typeItem, { [styles.active]: current === island.name })}
            onSelect={() => setRoundaboutIsland(island.name)}
          >
            {island.icon !== "" && <img className={styles.typeIcon} src={island.icon} />}
            <span>{island.label}</span>
          </Button>
        ))}
      </div>
      <div className={styles.hint}>Click a junction to place it. Clicking a junction that already has this island removes it.</div>
    </>
  );
};
