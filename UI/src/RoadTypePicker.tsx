import { useState } from "react";
import classNames from "classnames";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button } from "cs2/ui";
import { buildType$, roadTypes$, setBuildType } from "bindings";
import styles from "./ToolPanel.module.scss";
import { Section } from "./controls";

type RoadType = { kind: string; name: string; icon: string; label: string };

const kinds = [
  { kind: "Road", label: "Roads" },
  { kind: "Track", label: "Tracks" },
  { kind: "Pathway", label: "Paths" },
];

/**
 * Chooses the road type new roads are built as, shared by Connect, Parallel, Roundabout, Ramp and
 * Helix. "Same as the road" keeps each road's own type. Types of another kind than the road being
 * built from (a track off a road) are ignored by the tools.
 */
export const RoadTypePicker = ({ onCopyFromRoad }: { onCopyFromRoad?: () => void }) => {
  const raw = useValue(roadTypes$);
  const current = useValue(buildType$);
  const { translate } = useLocalization();
  const [open, setOpen] = useState(false);
  const [kind, setKind] = useState("Road");
  const [search, setSearch] = useState("");

  const label = (name: string) => translate(`Assets.NAME[${name}]`, name) ?? name;
  const types: RoadType[] = raw
    .split("\n")
    .filter((line) => line !== "")
    .map((line) => {
      const [k, name, icon] = line.split("\t");
      return { kind: k, name, icon: icon ?? "", label: label(name) };
    });
  const query = search.trim().toLowerCase();
  const shown = types
    .filter((t) => t.kind === kind && (query === "" || t.label.toLowerCase().includes(query)))
    .sort((a, b) => a.label.localeCompare(b.label));

  return (
    <>
      <Section title="Build as" />
      <div className={styles.row}>
        <span className={styles.label}>{current === "" ? "Same as the road" : label(current)}</span>
        <Button variant="flat" className={classNames(styles.choice, { [styles.active]: open })} onSelect={() => setOpen(!open)}>
          Choose
        </Button>
        {onCopyFromRoad && (
          <Button variant="flat" className={styles.choice} onSelect={onCopyFromRoad}>
            Copy from a road
          </Button>
        )}
        {current !== "" && (
          <Button variant="flat" className={styles.choice} onSelect={() => setBuildType("")}>
            Same as road
          </Button>
        )}
      </div>
      {open && (
        <>
          <div className={styles.row}>
            {kinds.map((k) => (
              <Button key={k.kind} variant="flat" className={classNames(styles.choice, { [styles.active]: kind === k.kind })} onSelect={() => setKind(k.kind)}>
                {k.label}
              </Button>
            ))}
          </div>
          <div className={styles.row}>
            <input className={styles.search} type="text" placeholder="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <div className={styles.typeList}>
            {shown.length === 0 && <div className={styles.hint}>No types found.</div>}
            {shown.map((t) => (
              <Button
                key={t.name}
                variant="flat"
                className={classNames(styles.typeItem, { [styles.active]: current === t.name })}
                onSelect={() => {
                  setBuildType(t.name);
                  setOpen(false);
                }}
              >
                {t.icon !== "" && <img className={styles.typeIcon} src={t.icon} />}
                <span>{t.label}</span>
              </Button>
            ))}
          </div>
        </>
      )}
    </>
  );
};
