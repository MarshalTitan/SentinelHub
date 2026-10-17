# Hub architecture and verified integration boundaries

Inspected live sources 2026-10-16 UTC: Sentinel, SentinelCore, SRankSentinel, PvPSentinel, SentinelHUD, ClassySentinel, SentinelProfiles, SentinelRelay, plus the historical SentinelHunts test identity within SRank; the user has retired that test component from Hub inventory. Canonical ecosystem documents were read before implementation; this explicit Hub MVP request reprioritizes their historical deferral without releasing any navigation gate.

- Ecosystem: fixed known component/dependency catalog, public installed-plugin discovery, pure status policy, fixed quick-open commands.
- Diagnostics: bounded typed fault journal; allowlisted JSON projection. It never exports arbitrary exceptions, paths, consumer configuration, credentials or chat. No consumer fault aggregation is fabricated.
- UI: bundled Core owns shell, style, rail, cards, switches, pills and ambient motion. Hub owns pages, window state and content. Stable window identity lets Dalamud preserve position/size; expanded dimensions are retained during Modern collapse.
- Configuration: local Hub theme/page/reduced-motion/expanded-size settings plus default-OFF Anti-AFK opt-in. Explicit import copies only the public HUD Anti-AFK bool; HUD configuration is never read privately or rewritten.
- Utilities: 0.2.0.0 adds the isolated default-OFF Anti-AFK engine. Synchronous framework-thread checks retain HUD's 10-second interval and 30-second threshold, with finite timer validation. Quest Convenience follows as the separate 0.3 transition below; Latency/Weaving remains inactive.
- Anti-AFK ownership: optional primitive IPC v1 with HUD 0.8.4.6+, per-session in-memory claims, synchronous HUD suspension, 3-second renewable leases and a hot-load request guard. Installed legacy/unknown HUD blocks Hub. Disable/logout/unload stops Hub before lease release. HUD 0.8.4.6/0.8.4.7 retains its engine; HUD 0.8.5.0 retains only preferences and compatible IPC. Disable Hub with the latter and actions stay off.
- Dependencies: vnavmesh.Nav.IsReady distinguishes responding/not-ready/ready. Lifestream.IsBusy, RotationSolverReborn.AutorotationActive and Splatoon.IsLoaded establish response only; idle/off does not mean missing or unresponsive. Splatoon geometry readiness and HuntAlerts/Sonar runtime health remain unknown. All queries are on the framework thread every two seconds.
- Profiles: public runtime status IPC is absent in the inspected Plugin.cs; selection, last-applied and drift remain unavailable. No private-file reading or profile application.
- Quick-open: fresh loaded-state and restriction check, then only source-verified registered commands. HUD/Profiles/Relay commands can toggle an already-open window. PvP diagnostic command is exposed; other diagnostic commands are not guessed.
- Runtime readiness: installation, loaded state, responding IPC, mesh ready and consumer gameplay-ready are distinct. Consumer enabled-automation flags and connectivity are not inferred from plugin load.
- Failure isolation: discovery failure invalidates current observations; individual dependency IPC exceptions become unavailable; save/export/command failures remain local typed faults.

Hub does not bundle consumer assemblies. The optional HUD compatibility bridge is published separately; HUD camera/visual behavior remains independent. HUD 0.8.5.0 runs no utility engine without Hub. Anti-AFK accesses the native inactivity timer module; Quest uses dedicated native addons as described below. No latency engine is introduced.

## Quest Convenience transition — 0.3

The user reported PASS for the latest four-step Anti-AFK procedure (active/managed, off returns HUD, Hub reload, idle beyond configured interval). This is scoped human evidence on the reported current pair, not blanket certification of unreported scenarios. Anti-AFK is unchanged.

Quest Convenience is default-OFF with its own request, lease and engine. HUD 0.8.4.7's optional v1 primitive IPC exposes a packed public preference integer and token-scoped claim/release. It retains its engine/schema/preferences and clears pending cutscene/reward state synchronously before acknowledging; per-update request guarding covers hot reload. Old Hub 0.2 never requests quest ownership. The two utility leases are independent.

Hub copies the accepted native service/policies from HUD 758068f68a76d72660fb6e2740f22d592130fdd7 with namespace/config adapter changes only; CI compares the complete text against that pinned source. The base-addon/live-ID submarine fix, prompt ownership/timeout and reward policy/fallback semantics remain intact. A pure execution session clears pending work on ownership loss, option changes, logout/unload and native exceptions, with a one-second fault retry bound. No tasks or stale native continuations exist. The native DiagnosticBuffer is bounded/local and excluded from export; exported quest data is only a typed module state.

Import is explicit and copies only three public quest preferences, without modifying saved HUD values or Anti-AFK. Module/master OFF stops Hub before lease release. The latest dialogue/choice/cutscene/reward/off/reload checklist received a scoped user PASS; reward mode unspecified and submarine not separately confirmed. Hub 0.3.1.0 changes guidance only; accepted native code, settings and controllers are unchanged. HUD 0.8.5.0 separately removes duplicate engines and retains v1 import/claim compatibility with no game-facing callbacks. Cleanup runtime acceptance is pending under UTILITY_CLEANUP.md. No latency or other consumer migration is included.

## Inventory selection — 0.3.1.1

Public IExposedPlugin.IsDev and Version are captured without reflection/private configuration. For known component rows, a matching non-dev installation wins over dev copies regardless of enumeration order or load state. Version/state/source all come from the selected instance. A dev-only row is labeled explicitly. CanOpen applies the same policy to fresh discovery so a loaded dev cannot substitute for disabled/restricted live. Dependency lookup/probe behavior is unchanged. Utility controllers and settings are unchanged.

The user reported cleanup PASS at 2026-10-16 01:38 America/Toronto, with the separate SRank version-display issue. Only the focused inventory acceptance remains for this patch.

## Latency / Weaving — 0.4.0.0

User explicitly requested the NoClippy-style module on 2026-10-16 01:55 America/Toronto. Its separate [design/source audit and native proving gate](LATENCY_WEAVING.md) follows the accepted utility cleanup. The live/dev inventory display check remains separate.

LatencyMode adds Off=0/Measure=1/Compensate=2 without changing schema 1 or existing preferences. Native runtime creation is lazy behind explicit mode, login/transition/death/territory and conflict eligibility. Native request/effect delegates use current typed FFXIVClientStructs bindings. A generic nonallocating intercepted-call transaction is shared by the adapter and tests: observer failures never skip/repeat Original; original exceptions are not replayed.

Managed natural-cast/category metadata is cached before installing hooks. The single pending action stores only numeric sequence/action/time/initial lock; no pointer survives an inline call. Matching response validation and bound calculation live in pure LatencySession; actual writes alone increment applied counts. Full native conflict checks precede eligible response writes. Off or loss of readiness disposes both hooks; construction is transactional. Fault/contention latches until Off. Hook callbacks and disposal share a reentrant lock so teardown cannot free a trampoline in an executing callback.

Only the local ActionManager.AnimationLock field can be written, after Original Receive. Header, targets, cooldowns and action arguments are never modified; no task, packet-send hook or synthetic action exists. Unsupported/malformed/late responses are ignored. The source-supported narrow subset intentionally excludes natural cast-time actions and nonstandard locks. Conflict checks are scoped to known identities/exports plus observed-lock invariants; they do not promise detection of every custom mod.

The native adapter is new, unlike the preserved Quest engine. Policy/source/compilation/render evidence cannot replace the dedicated real-game Measure→Compensate gate. Existing Anti-AFK/Quest native code, Core/UI 0.4.1 pin, HUD and consumer projects are unchanged.

## Passive Mount Collection — 0.5.0.0

Appended page preserves all prior persisted page IDs. MountGameDataBuilder traverses local sheets in bounded framework-thread batches; no network/task continuations. MountPolicy handles three-state knowledge/ownership, filtering, stable sorting, completion, exact-name metadata resolution, exchange eligibility, safe inventory aggregation and stale lookup validation. SentinelMountMetadata owns six independently authored factual tracks with official provenance. No acquisition/expansion/obtainability heuristic fills missing metadata.

MountController owns a separate lazy catalog and ephemeral per-character observations, clears them on logout/character/territory/readiness/failure, refreshes visible counts at most every two seconds and isolates errors through a typed fault. Public Dalamud interfaces handle unlocks/current mount/context/icons; the only direct native adapter copies four loaded 35-slot main bags synchronously with layout/quantity/symbolic guards. Context subscriptions are removed on disposal, and callbacks revalidate lifetime/setting/session/territory/player before opening details. No native hook, target tooltip, arbitrary action, item/market request or private consumer config is involved. Other utility engines/settings, diagnostics payload and Core/UI pins are unchanged. See MOUNT_COLLECTION_METADATA.md for exact coverage and MOUNT_COLLECTION.md for native proving.
