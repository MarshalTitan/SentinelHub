# Hub architecture and verified integration boundaries

Inspected live sources 2026-10-16 UTC: Sentinel, SentinelCore, SRankSentinel, PvPSentinel, SentinelHUD, ClassySentinel, SentinelProfiles, SentinelRelay, plus the historical SentinelHunts test identity within SRank; the user has retired that test component from Hub inventory. Canonical ecosystem documents were read before implementation; this explicit Hub MVP request reprioritizes their historical deferral without releasing any navigation gate.

- Ecosystem: fixed known component/dependency catalog, public installed-plugin discovery, pure status policy, fixed quick-open commands.
- Diagnostics: bounded typed fault journal; allowlisted JSON projection. It never exports arbitrary exceptions, paths, consumer configuration, credentials or chat. No consumer fault aggregation is fabricated.
- UI: bundled Core owns shell, style, rail, cards, switches, pills and ambient motion. Hub owns pages, window state and content. Stable window identity lets Dalamud preserve position/size; expanded dimensions are retained during Modern collapse.
- Configuration: local Hub theme/page/reduced-motion/expanded-size settings plus default-OFF Anti-AFK opt-in. Explicit import copies only the public HUD Anti-AFK bool; HUD configuration is never read privately or rewritten.
- Utilities: 0.2.0.0 adds the isolated default-OFF Anti-AFK engine. Synchronous framework-thread checks retain HUD's 10-second interval and 30-second threshold, with finite timer validation. Other utilities remain inactive plans.
- Anti-AFK ownership: optional primitive IPC v1 with HUD 0.8.4.6+, per-session in-memory claims, synchronous HUD suspension, 3-second renewable leases and a hot-load request guard. Installed legacy/unknown HUD blocks Hub. Disable/logout/unload stops Hub before lease release. HUD retains its engine and saved preference pending supervised acceptance; see ANTI_AFK_MIGRATION.md.
- Dependencies: vnavmesh.Nav.IsReady distinguishes responding/not-ready/ready. Lifestream.IsBusy, RotationSolverReborn.AutorotationActive and Splatoon.IsLoaded establish response only; idle/off does not mean missing or unresponsive. Splatoon geometry readiness and HuntAlerts/Sonar runtime health remain unknown. All queries are on the framework thread every two seconds.
- Profiles: public runtime status IPC is absent in the inspected Plugin.cs; selection, last-applied and drift remain unavailable. No private-file reading or profile application.
- Quick-open: fresh loaded-state and restriction check, then only source-verified registered commands. HUD/Profiles/Relay commands can toggle an already-open window. PvP diagnostic command is exposed; other diagnostic commands are not guessed.
- Runtime readiness: installation, loaded state, responding IPC, mesh ready and consumer gameplay-ready are distinct. Consumer enabled-automation flags and connectivity are not inferred from plugin load.
- Failure isolation: discovery failure invalidates current observations; individual dependency IPC exceptions become unavailable; save/export/command failures remain local typed faults.

Hub does not bundle consumer assemblies. The optional HUD compatibility bridge is published separately; Hub absence preserves HUD behavior. Anti-AFK alone accesses the native inactivity timer module. No game hooks, action invocation, chat sending, Quest Convenience migration or latency compensation is introduced.

## Quest Convenience transition — 0.3

The user reported PASS for the latest four-step Anti-AFK procedure (active/managed, off returns HUD, Hub reload, idle beyond configured interval). This is scoped human evidence on the reported current pair, not blanket certification of unreported scenarios. Anti-AFK is unchanged.

Quest Convenience is default-OFF with its own request, lease and engine. HUD 0.8.4.7's optional v1 primitive IPC exposes a packed public preference integer and token-scoped claim/release. It retains its engine/schema/preferences and clears pending cutscene/reward state synchronously before acknowledging; per-update request guarding covers hot reload. Old Hub 0.2 never requests quest ownership. The two utility leases are independent.

Hub copies the accepted native service/policies from HUD 758068f68a76d72660fb6e2740f22d592130fdd7 with namespace/config adapter changes only; CI compares the complete text against that pinned source. The base-addon/live-ID submarine fix, prompt ownership/timeout and reward policy/fallback semantics remain intact. A pure execution session clears pending work on ownership loss, option changes, logout/unload and native exceptions, with a one-second fault retry bound. No tasks or stale native continuations exist. The native DiagnosticBuffer is bounded/local and excluded from export; exported quest data is only a typed module state.

Import is explicit and copies only three public quest preferences, without modifying saved HUD values or Anti-AFK. Module/master OFF stops Hub before lease release. Native acceptance remains pending under QUEST_CONVENIENCE_MIGRATION.md; no HUD cleanup, latency or other consumer migration is included.
