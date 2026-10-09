import { bindValue, trigger } from "cs2/api";
import mod from "mod.json";

export type ToolId = "None" | "AddNode" | "RemoveNode" | "Slope" | "Connect" | "Parallel";

export const panelOpen$ = bindValue<boolean>(mod.id, "PanelOpen", false);
export const activeTool$ = bindValue<ToolId>(mod.id, "ActiveTool", "None");
export const slopeProfile$ = bindValue<number>(mod.id, "SlopeProfile", 0);
export const connectMode$ = bindValue<number>(mod.id, "ConnectMode", 0);
export const parallelOffset$ = bindValue<number>(mod.id, "ParallelOffset", 16);
export const parallelHeight$ = bindValue<number>(mod.id, "ParallelHeight", 0);
export const parallelReverse$ = bindValue<boolean>(mod.id, "ParallelReverse", false);

export const togglePanel = () => trigger(mod.id, "TogglePanel");
export const selectTool = (tool: ToolId) => trigger(mod.id, "SelectTool", tool);
export const setSlopeProfile = (value: number) => trigger(mod.id, "SetSlopeProfile", value);
export const setConnectMode = (value: number) => trigger(mod.id, "SetConnectMode", value);
export const setParallelOffset = (value: number) => trigger(mod.id, "SetParallelOffset", value);
export const setParallelHeight = (value: number) => trigger(mod.id, "SetParallelHeight", value);
export const setParallelReverse = (value: boolean) => trigger(mod.id, "SetParallelReverse", value);
