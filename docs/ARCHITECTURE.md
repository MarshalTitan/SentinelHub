# Hub architecture and verified integration boundaries

Inspected live sources 2026-10-16 UTC: Sentinel, SentinelCore, SRankSentinel, PvPSentinel, SentinelHUD, ClassySentinel, SentinelProfiles, SentinelRelay, plus SentinelHunts within SRank. Canonical ecosystem documents were read before implementation; this explicit Hub MVP request reprioritizes their historical deferral without releasing any navigation gate.

- Ecosystem: fixed known component/dependency catalog, public installed-plugin discovery, pure status policy, fixed quick-open commands.
- Diagnostics: bounded typed fault journal; allowlisted JSON projection. It never exports arbitrary exceptions, paths, consumer configuration, credentials or chat. No consumer fault aggregation is fabricated.
- UI: bundled Core owns shell, style, rail, cards, switches, pills and ambient motion. Hub owns pages, window state and content. Stable window identity lets Dalamud preserve position/size; expanded dimensions are retained during Modern collapse.
- Configuration: local Hub-only theme/page/reduced-motion/expanded-size settings. No cross-plugin preference service or runtime coupling.
- Utilities: inactive plans only. Anti-AFK is the recommended next isolated migration, with default OFF and explicit single-owner control before any HUD cleanup.
- Dependencies: vnavmesh.Nav.IsReady distinguishes responding/not-ready/ready. Lifestream.IsBusy and RotationSolverReborn.AutorotationActive establish response only; a false value does not mean failure. Splatoon/HuntAlerts/Sonar remain loaded/unknown. All queries are on the framework thread every two seconds.
- Profiles: public runtime status IPC is absent in the inspected Plugin.cs; selection, last-applied and drift remain unavailable. No private-file reading or profile application.
- Quick-open: fresh loaded-state and restriction check, then only source-verified registered commands. HUD/Profiles/Relay commands can toggle an already-open window. PvP diagnostic command is exposed; other diagnostic commands are not guessed.
- Runtime readiness: installation, loaded state, responding IPC, mesh ready and consumer gameplay-ready are distinct. Consumer enabled-automation flags and connectivity are not inferred from plugin load.
- Failure isolation: discovery failure invalidates current observations; individual dependency IPC exceptions become unavailable; save/export/command failures remain local typed faults.

No consumer repository is modified by Hub's runtime implementation. No game hooks, native memory access, action invocation, chat sending or utility engine is present.
