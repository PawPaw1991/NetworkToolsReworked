# Network Tools Reworked

A Cities: Skylines II mod for precise network editing: add, remove and slide nodes, reshape slopes and curves, connect, loop, parallel and generate road, path, rail and waterway networks.

It is a from-scratch rework inspired by the ideas in CS1's Network Multitool and CS2's Network Tools. It contains no code from either.

## Design rule

**Every edit goes through the game's tool pipeline.** Tools emit `CreationDefinition` + `NetCourse` definition entities (with `m_Original` set when modifying existing networks, and correct start/end elevations). Vanilla systems then build the `Temp` preview, validate it and apply it.

Tools never write `Game.Net.Node`, `Edge`, `Curve`, `Composition` or `Elevation` on live entities, and never add `Deleted` directly. Doing so leaves geometry, composition, lanes and the utility flow graph out of sync, which shows up as gray or invisible roads after a reload and as crashes.

## Planned tools

| Tool | Notes |
|---|---|
| Add / Remove node | Remove keeps elevation on bridges and tunnels |
| Slide / Move node | Move along an edge or freely |
| Slope | Linear, ease-in-out, arch; respects clearance over and under other networks |
| Curve | Straighten, smooth |
| Connect | Simple curve, complex curve, loop; rotatable start node |
| Parallel | Offset, vertical offset, same or opposite direction |
| Generate | Grid, circle, oval |
| Undo | Snapshot of originals touched by the last apply |

## Using it (current build)

Click the **Network Tools** button at the top left of the screen to open the tool panel. Pick a tool there and set its options. The keys below do the same.

| Key | Tool |
|---|---|
| Ctrl+N | Add Node: hover a road, path or track and click to split it with a new node |
| Ctrl+Shift+N | Remove Node: hover a node joining two segments of the same type and click to merge them |
| Ctrl+G | Slope: click a start node, hover an end node to preview, click to re-grade the road between them. The shape (linear or ease in/out) is set in Options |
| Ctrl+J | Connect: click a start node, hover an end node to preview a new road between them, click to build it. `,` and `.` rotate the start direction |
| Ctrl+Shift+P | Parallel: click a start node, hover an end node to preview a copy of the road between them, click to build it. Side offset, height offset and direction are set in Options |

Press the key again or right-click to leave the tool. Both keys can be rebound in Options.

## Building

Requires the official Cities: Skylines II modding toolchain (set up from the game's Options > Modding), which provides `CSII_TOOLPATH`, `Mod.props` and `Mod.targets`.

```
dotnet build NetworkToolsReworked/NetworkToolsReworked.csproj -c Release
```

The build also builds the UI panel in `UI/` with npm (installing its packages the first time) and deploys both to the local mods folder.
