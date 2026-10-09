import classNames from "classnames";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { panelOpen$, togglePanel } from "bindings";
import icon from "images/NetworkTools.svg";
import styles from "./ToolPanel.module.scss";

export const ToolButton = () => {
  const open = useValue(panelOpen$);
  return (
    <Tooltip tooltip="Network Tools Reworked">
      <Button variant="floating" className={classNames(styles.toggle, { [styles.selected]: open })} onSelect={togglePanel}>
        <img style={{ maskImage: `url(${icon})` }} />
      </Button>
    </Tooltip>
  );
};
