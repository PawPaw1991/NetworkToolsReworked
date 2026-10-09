import { ModRegistrar } from "cs2/modding";
import { ToolButton } from "ToolButton";
import { ToolPanel } from "ToolPanel";

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", ToolButton);
  moduleRegistry.append("Game", ToolPanel);
};

export default register;
