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
| Ctrl+G | Slope & Curve: pick two nodes to re-grade, smooth or straighten the road between them. Options are in the tool panel |
| Ctrl+Shift+G | Smooth: pick two nodes to smooth the road between them. Options are in the tool panel |
| Ctrl+Shift+D | Move Node: click a node, drag it with the cursor, click to drop, then nudge its position and height in the panel and apply. The roads attached to it follow |
| Ctrl+Shift+A | Arrange: pick two nodes to space the nodes between them evenly, along the current shape, on a straight line, or on an arc (with adjustable bulge) |
| Ctrl+Shift+R | Reverse: pick two nodes to reverse the direction of the road between them (one-way roads, tracks). Side-specific upgrades stay on the same side |
| Ctrl+Shift+O | Roundabout: click a junction of three or more roads to preview a roundabout around it, set the radius and direction in the panel, then apply |
| Ctrl+Alt+Z | Undo: preview undoing the last edit made with these tools (red is removed, green restored), then click or press Apply. Keeps the last 30 edits for the session. Ctrl+Z is left to other mods such as Move It |
| Ctrl+Shift+X | Intersect: hover a road near where another crosses it without a junction, click to preview a junction there, then apply. Roads more than 2 m apart in height are left alone |
| Ctrl+Shift+T | Change road type: click a road of the type you want, then pick two nodes. The road between them becomes that type, keeping its shape and height. Choose in the panel whether upgrades are kept. Roads aren't swapped for tracks or paths |
| Ctrl+Shift+U | Copy upgrades: click a road to copy its upgrades (trees, sidewalks, sound walls, lighting...), then pick two nodes to give the road between them the same set. A road without upgrades clears them. Swap left and right in the panel for roads drawn the other way |
| Ctrl+Shift+M | Measure: hover a road to see its length, grade at the cursor, steepest grade against the road type's limit (steeper parts in red), tightest curve radius and height above ground. Click two nodes to measure between them: length along the road, straight-line distance and bearing, height difference, average and steepest grade, tightest curve and how far the road turns. Changes nothing |
| Ctrl+Shift+E | Ramp: hover a road to preview a ramp leaving (exit) or joining (entry) it at the cursor, click to lock it, then set the side, the road direction it follows, the angle off the road, a further turn, the length and the end height in the panel. The ramp is lengthened when needed to stay within its road type's grade limit. It uses the road's own type unless you copy another one from a road |
| Ctrl+Shift+H | Match height: click a node to take its height (fine-tune it in the panel), then click other nodes to move them to that height, one after another. Nodes stay where they are on the map and their roads follow |
| Ctrl+J | Connect: click a start node, hover an end node to preview a new road between them, click to build it. `,` and `.` rotate the start direction |
| Ctrl+Shift+P | Parallel: click a start node, hover an end node to preview a copy of the road between them, click to build it. In the panel, space it in metres, touching the road (edge to edge plus a gap) or in whole road widths, on the left, right or both sides, with a height offset and direction |

Slope, Connect and Parallel work in three steps, shown in the panel: click a start node, hover an end node and click it to lock the preview, then review it. While the preview is locked you can change the tool's options in the panel and the preview updates; click again or press **Apply** to build it, or right-click / **Back** to pick another end. The picked nodes and the affected roads are highlighted (green start, orange end, red when the target can't be used), and the panel shows length, height change and grade.

**Slope & Curve options** (tool panel):

- Slope: Keep, Linear or Ease in/out, with the ease length (5 to 50% of the road at each end), an arch (bump or dip at the middle), and start and end height changes in 0.1 m or 1 m steps.
- Curve: Keep, Smooth (line up the direction at every joint; optionally keep the direction at both ends so roads beyond stay aligned), Straighten, or Transition (a highway-style bend that tightens gradually from the road's direction at each end, like a transition spiral; inner nodes move along it, so it works best over three or more segments), each with a strength from 0 to 100%.
- The preview is coloured by grade against each road's own limit (green, yellow, red) and the panel shows the steepest grade. When no node moves (for example smoothing only), the roads are edited in place and keep their identity.

**Smooth options** (tool panel): strength (0 to 100%), keep the direction at both ends or leave them free, smooth the slope too (evens out bumps and dips at the nodes) or curves only, and Relax nodes, which also pulls the inner nodes towards an even line. Without Relax no node moves, so the roads are edited in place. The preview shows the current road as a dashed line under the new shape.

**Move Node snapping** (tool panel): no snap, a world grid (0.5 to 32 m), or 15° steps and whole metres from the node's original spot, lined up with its first road.

**Clearance warning** (Options, default 6 m, 0 turns it off): Slope & Curve and Smooth mark in red any spot where the reshaped road passes closer than this above or below another road, and say so in the panel. The game's own checks still decide what can be built.

Press the key again or right-click to leave the tool. Both keys can be rebound in Options.

## Building

Requires the official Cities: Skylines II modding toolchain (set up from the game's Options > Modding), which provides `CSII_TOOLPATH`, `Mod.props` and `Mod.targets`.

```
dotnet build NetworkToolsReworked/NetworkToolsReworked.csproj -c Release
```

The build also builds the UI panel in `UI/` with npm (installing its packages the first time) and deploys both to the local mods folder.
