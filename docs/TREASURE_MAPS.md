# Treasure Maps — 0.8.1.0

Treasure Maps is an optional Hub page and independent floating queue. It is **Off by default** on new installations and upgrades. Existing utility settings, page IDs, performance scheduling and Core/UI 0.4.1 are preserved. Page IDs 12–15 remain reserved for retired candidates; the new page uses 16.

## Use

Open `/shub` → Treasure Maps and enable the module. Party map flags are captured by default; Alliance capture is a separate opt-in. Other channels, plain coordinate text and messages without a structured player sender are ignored. Original chat is never modified or suppressed. Other party members need no plugin.

Each location retains its exact territory ID, **specific map ID**, raw signed coordinates, map-link payload bytes, owner name/home world and receipt time. Display names and coordinate conversions come from current local game sheets. Reopening uses the original raw coordinates, not rounded display values or a territory's default map.

- **Open Flag / Next Map** establishes the saved flag on the world map. No teleport, navigation or character movement is requested.
- **Share** establishes that exact flag and copies `/p <flag>` to the clipboard. Paste it into chat and press Enter to send a real clickable game map link. This is an explicit manual-send fallback: Dalamud's public chat service prints local messages and does not offer a supported party-send API. Hub does not invoke a private chat-send function or silently substitute plain coordinates. Changing the game's flag before sending also changes what `<flag>` will send.
- **Complete** immediately removes the entry on the next framework update; the next map moves up. **Undo Complete** restores its identity and previous index for 15 seconds. Only one Undo slot exists in RAM.
- Every row in both views offers **Open Flag**, **Share**, **Complete**, visible **Up/Down arrows** and **More...**. Controls wrap onto another line at narrow widths. Up is disabled on the first row; Down is disabled on the final row. Reordering uses the existing persisted character queue, so Next Map always opens its current first entry.
- **More...** offers Delete, Move Up, Move Down and Add another in an independent native ImGui popup. Menu items size to their labels and ImGui repositions the popup inside the viewport. Delete creates no completion record. Add another supports an intentional second map from the same owner at the same location. The floating view includes all unfinished rows in its scroll area, with the same actions as Hub; only receipt timestamps are omitted for compactness.
- Clear Entire Queue requires confirmation and affects only the active character. There is no completed-map archive.

Duplicate identity is owner name plus home world and exact territory/map, with a two-raw-unit tolerance (0.002 world units) solely to absorb flag float round-trips. Stored coordinates and payload bytes are never rounded or altered. Different owners remain independent. Different flags from one owner remain independent. A 30-second explicit-share ticket ignores only the local echo of the prepared flag; it does not suppress another player's matching flag. Identical repeated chat payloads cannot establish whether the same owner has a second physical map, so Add another is available instead of guessing.

## Queue links and icon menus (0.8.1.1)

Both views draw the same owner/location link with a grip icon. Click it to open the saved flag, or drag it onto another link. The upper/lower half inserts before/after that map, with a placement line during the preview. Native ImGui handles drag capture, scrolling and cancellation; drags can cross between queue windows. These are internal queue drags, not structured chat attachments. Share retains its existing manual `/p <flag>` preparation.

A delivered drop submits one framework-thread request containing source and target entry identities, rather than a burst of Up/Down actions. A 200-entry queue can be rearranged in one operation. Missing/stale identities, self-drops and already-adjacent no-ops preserve the current snapshot and schedule no save. Previews never change or save the queue. Existing Up/Down controls remain available. The same character-scoped list persists the result; Next Map, Complete and Undo use that order.

More uses the existing bundled Font Awesome font for Delete, Move Up, Move Down and Add another icons. Its native MenuItem hit boxes include the reserved icon column and full labels, retaining independent popup sizing, viewport placement and clipping. No image downloads or dependencies were added. Tests check actual glyph availability, popup layout and clicks, link clicks, native mouse drops, cancellation, self/disabled/stale drops, persistence, Next Map and the existing X/Show/Hide lifecycle. Supervised in-game checks should also exercise both views at the active UI scale; fixture results do not certify real game rendering or movement performance.

## Queue card styling (0.8.2.0)

Both queue views use the same dark card palette, teal route/hover accents, blue underlined location links, grip handles and outlined icon action buttons. Wide cards put metadata and controls side by side; narrow cards stack them and wrap controls. Map names, location labels and receipt timestamps wrap within their own measured area. Main Hub keeps receipt timestamps; compact mode omits them. The floating window uses matching dark native chrome while retaining its existing close/collapse controls and geometry.

The ellipsis button opens the existing independent native popup. All row actions, drag/link behavior and controller requests are retained. Card backgrounds use ordinary draw-list rectangles; no clipped child window or additional per-card window is introduced. Presentation arrays cache IDs, formatted labels/timestamps and measured geometry until the queue snapshot, width, font/scale or relevant style metrics change. Caption/glyph strings are reused. Cache work occurs only while a view draws; controller scheduling and dormant behavior are unchanged.

Additional native fixtures cover long text, card bounds, wide inline and narrow stacked layouts, actual ellipsis mouse clicks, rendered card/link colors, glyph availability, cached presentation reuse/invalidation and balanced style/font stacks. Popup coverage now includes a 900-pixel wide case alongside narrow sizes. The existing full Hub/floating, drag, Next/Complete/Undo, persistence, X/Show/Hide and idle suites remain required. An offline render of native draw data was visually inspected; it is fixture evidence rather than an in-game screenshot or gameplay performance acceptance.

## Core window shell (0.8.2.1)

The floating window now uses the pinned Core/UI 0.4.1 `SentinelModernAppShell`, `SentinelModernStyleScope.PushAppShell` and `SentinelModernWindowChrome`, matching Hub's existing Modern 2 shell. The Core header, single map icon rail, unified midnight-navy canvas and ambient blue/violet/teal background surround the unchanged map cards. Compact content padding and minimum content dimensions keep the standalone queue usable; Core owns the scrollable page and non-scrolling header. No replacement shell or duplicated palette is introduced. Classic selection continues using a native title bar.

Core callbacks handle minimize/expand and close. The new optional `FloatingCollapsed` preference preserves header-only state and expanded geometry; it defaults false for existing configurations. Geometry stays in the existing native-pixel format. Before passing dimensions to Dalamud WindowHost, the adapter converts them to logical units so Host applies the UI scale exactly once. Minimize transitions never save the header height over the expanded height. X saves the shared visibility flag once, including Core and delayed native close paths. Closed/minimized views never draw or read the queue. Core's motion respects both Hub preferences and the current system reduced-motion provider; all shell/delegate state is retained per window and disposed on unload.

The card renderer, rounded dark surfaces, teal accents, blue links, outlined icon controls, responsive layouts and cached presentations are preserved, as are queue/controller and game/chat behavior. Native fixtures cover Core canvas/chrome/header/page, logical/native sizing across 1/1.2/1.5/2x scales, both reduced-motion modes, real minimize/expand/X clicks, expanded geometry, JSON recovery, Classic/Modern transitions and a recreated minimized window. Existing popup/action fixtures now target the real Core content child and its scroll area. All previous queue, drag, persistence and dormant checks remain required.

Implementation guidance: [Core Modern theme at the pinned tag](https://github.com/MarshalTitan/SentinelCore/blob/v0.4.1.0/docs/MODERN_THEME.md). Offline native draw-data renders were inspected; in-game visual and movement acceptance are not inferred from these fixtures.

## Automatic detection

The adapter observes the game's treasure-map display call and forwards the original call exactly once before observing anything. It never suppresses the normal treasure-map window. The rank/subrow pair resolves through `TreasureHuntRank` → `TreasureSpot` → `Level.Map`; this preserves map variants. Actual spot subrow zero is valid. Cached rank/item and map/territory metadata is resolved only on demand.

There is no current public Dalamud decipher/treasure-spot event. The optional observer now matches the treasure caller's rank/spot/mode register setup and separately validates the callee's argument reads, as well as signature uniqueness, signed relative CALL target and executable-text bounds. The generic UI singleton anchor used in 0.8.0.0 matched an unrelated one-argument function; it has been removed. A plausible CALL target inside executable memory alone is insufficient proof of function identity.

The corrected binding was checked offline against game build `2026.10.16.0000.0000`: exactly one call-site match (RVA `8E2F94`) resolved to the treasure display handler (RVA `CFB710`), and the handler reads the expected manager, rank, spot and mode registers. The maintained implementation contains a signature and semantic instruction checks, not those fixed addresses. No game binary or full disassembly is shipped. Setup failure latches unavailable with the failing binding stage, leaving chat capture usable. Offline instruction inspection does not establish actual in-game opening, correct treasure coordinates or felt performance.

No ActorControl packet parser, unmanaged treasure-manager pointer, old rank/spot getter signature or external dataset is imported. Native observation copies only rank/subrow numbers to a bounded queue. Game sheets, map APIs and UI work run from Hub's framework update. Detection is installed only when the module and detection option are enabled and a character is ready. Off/logout/unload disables or disposes it; it does not rescan every frame. An unavailable observer leaves structured chat capture and saved queue actions usable.

Public key-item added/removed notifications distinguish a newly deciphered item from an ordinary existing-map view and clear an inapplicable current map. A 250-ms active-only classification delay allows either notification order while preserving separate decipher/view options. A saved current key is checked once at login or on manual Open My Map, never continuously. Unknown inventory readiness does not erase a saved map. Reopening the game's treasure-map item identifies it again after a reload. Login readiness retries are bounded to ten seconds.

## State and performance

Unfinished queues and the last identified current map are stored in the existing local Hub configuration under the active content-ID key. Characters have independent queues; logout clears the runtime view and transient work without deleting persisted entries. No content ID, player name or queue payload is added to diagnostics exports. Floating visibility and geometry are saved as ordinary Hub UI preferences. The full page and floating window read the same controller snapshot.

The floating queue's title-bar X and Hub's Show/Hide Floating Queue button control the same persisted visibility setting. Closing with X returns Hub to Show Floating Queue; clicking Show reopens the window at its remembered geometry. Closing the window preserves all unfinished entries. The X-close regression uses native ImGui input and the real panel buttons across both themes and three UI scales.

Default capacity is 50, configurable from 5 to 200. A full queue rejects new additions without evicting unfinished maps. Lowering the limit never silently trims existing entries. Invalid persisted rows are rejected independently while valid entries survive. Undo and share tickets are not persisted. Changes are saved on a coalesced 300-ms deadline and flushed on logout/unload; there is no periodic queue file write.

Idle `Tick` returns before clocks, framework/service reads, metadata, inventory or allocation. There is no chat-history scan, full inventory search, IPC, network operation, timer thread or background game-API task. Relevant events and explicit actions are bounded to 128 pending items and 32 processed per framework update. Closed windows do not draw queue UI. Visible UI has normal layout/rendering costs. `/shub perf start`, `report` and `stop` include a separate TreasureMaps service bucket.

Local fixture measurement: enabled idle was approximately 0.06 microseconds per guarded call and zero allocated bytes over 10,000 calls, with zero service reads after initialization. This is synthetic controller overhead, **not actual game frame time or proof of stutter-free gameplay**. Cloud CI repeats the regression fixtures.

## Reference and API audit

All four requested projects were inspected for behavior and interoperability ideas:

| Reference | Inspected source | Boundary |
|---|---|---|
| [Globetrotter](https://github.com/chirpxiv/Globetrotter) | `e5ac31b696c599cc00be4d042d12efe53ac3663f` | Rank/spot resolution and map-view behavior; inspected project targets SDK 13. No source, packet parser or old signatures copied. No standalone license was found in the inspected tree; no reuse permission is assumed. |
| [MapLinker](https://github.com/Bluefissure/MapLinker) | `c57a0c6942d06c84dfe0a8c303d584130d5e9dd6` | Structured links, owner/time and stored flags. Broad-channel capture, coordinate-only duplicate rules, default-map substitution and teleport features are not adopted. No code copied; no license grant assumed. |
| [SimpleMapTracker](https://github.com/Caraxi/SimpleMapTracker) | `1dd5f3d7918da8b859c8c5ceacdbff2085a9f588` | Party-oriented tracking. Its websocket service, external account/session data and unmanaged getters are not used. No code copied; no license grant assumed. |
| [MapPartyAssist](https://github.com/wrath16/MapPartyAssist) | `b332d0d46fc86eb5100e5e1e3088925bf8bfca2e` (MIT) | Owner associations and raw map fields. No implementation, database, loot/duty tracker or web service imported. |

Current public Dalamud sources inspected at `1e514694497ae448fca8e8a46f7b63642ad1e5ad`: `IChatGui`, `IGameGui`, `IGameInventory`, `ISigScanner`, `MapLinkPayload`, `PlayerPayload` and window lifecycle APIs. This is an independent implementation using those APIs. Normal dependency licenses remain applicable. Product builds/runtime do not fetch any reference repository or treasure dataset.

## Automated and supervised validation

Automated checks cover raw coordinate/map-variant preservation, real `MapLinkPayload` encode/decode/restore, rejection of text-only/unrelated channels/non-player labels, owner-aware duplicates and explicit copies, reorder/Complete/Undo/Delete/limits, character-scoped JSON recovery, stale queued actions after logout, framework-thread gating, subscription cleanup, save flushing, exactly-once native forwarding, twelve decipher/view notification-order scenarios, manual Share preparation and idle allocation/read guards. Native ImGui fixtures cover populated/empty/full queues, both themes, three UI scales, narrow/wide/narrow resizing and independent floating visibility. Existing Hub tests remain required.

**In-game verification procedure (not established by CI):**

1. Enable Treasure Maps on 0.8.1.0. Decipher a map; confirm the displayed flag against the actual treasure-map image and zone. Test another supported map type or a map variant where available. Independently toggle after-decipher and existing-map-view behavior.
2. Have party members link flags normally. Check each owner and exact location. Repeat a flag, share the same coordinates from two different owners, and post a second different map from one owner. Confirm unrelated channels/text are ignored and ordinary chat is unaffected.
3. Open a queued flag. Click Share, paste `/p <flag>` into party chat and send it. Confirm the sent link is clickable for another party member and opens the same location. Confirm the local echo does not add a duplicate or change ownership.
4. Complete a middle/first entry, Undo, reorder and Delete; test Clear confirmation. Open the floating window with the main Hub closed and verify both views stay synchronized.
5. Reload Hub/restart the game; unfinished entries and floating preferences remain. Switch characters and confirm queues do not mix. Preserve the existing settings file.
6. Compare movement/camera with normal Hub settings, both queue windows closed and then the floating queue visible. Optionally collect `/shub perf start` → normal movement → `/shub perf stop` output. A synthetic zero-allocation fixture is not a live performance PASS.

0.8.1.0 is distributed through the existing live plugin repository and normal Dalamud updates. No separate development installation or manual ZIP download is needed. These procedures document optional in-game verification and are not a claim that it has occurred. If the new module misbehaves, turn Treasure Maps Off; existing configuration, performance improvements and other Hub features remain intact. Published release assets remain immutable.
