# Performance and Profiles — 0.7.1.0

**Live release preparation — 2026-10-26.** Software review, CI and candidate-package validation passed. Movement/camera and interaction validation remains pending; no new in-game PASS or proven stutter fix is claimed. Historical 0.7.0.0 PASS reports do not certify this scheduling change. Profiles counterpart: 0.2.1.8; no configuration/schema migration or Core update.

## Investigation and fixture measurements

Live baseline: Quest scanned ownership every frame; closed/unready collections recreated observations/views; inactive Inspector recreated empty views; Activity rebuilt conflict sets every frame. Two independent latency probes expanded Process.Modules, including compatibility while latency was Off and forced checks on responses.

Windows runner fixture baseline [40397795028](https://github.com/MarshalTitan/SentinelHub/actions/runs/40397795028), source c042a7e485b6f0825113f20f41ea1af8033a2bf7; optimized [40401302359](https://github.com/MarshalTitan/SentinelHub/actions/runs/40401302359):

| Idle path | Baseline bytes/call | Optimized bytes/call |
|---|---:|---:|
| Quest Off | 624 | 0 |
| Mounts closed | 1,104 | 0 |
| Fashion closed | 1,104 | 0 |
| Inspector inactive | 184 | 0 |
| Activity unchanged | 384 | 0 |

Disabled Quest made 10,200 baseline inventory reads including warmup; optimized made zero. Forced full native scans averaged about 4.41 ms before, versus 0.45 ms (0.55 ms maximum) for the bounded handle/export scan. The latter fixture still included 208 bytes/call of its fallback fake inventory adapter; production uses the shared array. A later fixture uses the same shared-array adapter as production. One Modern dashboard fixture measured 0.37 ms average, 1.90 ms maximum and 3,432 bytes/frame while open. Runner timings vary; means include fixture-loop overhead. These are synthetic observations, **not game frame times or proof that stutter is eliminated**.

## Scheduling

| Service | Active/event work and bounded fallback |
|---|---|
| Inventory | Shared immutable observation; ActivePluginsChanged/open/explicit refresh; 60-second fallback, 5-second unavailable retry |
| Health/dependencies | Reuse inventory; 2-second readiness while Hub expanded, 15 seconds closed/collapsed |
| Quest | Off/logged-out stops/releases once. Addon PostSetup/PostRefresh/PreFinalize and cutscene-condition wakeups; 100-ms visibility fallback. Native engine only for relevant UI/cutscene/pending confirmation, plus exit cleanup. Cached-inventory ownership/legacy lease renewal at 1 second |
| Anti-AFK | No Off scans/IPC; shared inventory; existing 1-second ownership and 10-second native timer cadence |
| Compatibility | Inventory/mode changes/explicit refresh invalidate cache. Keep reviewed provider polling at 500 ms with Quest enabled because no reliable provider change event exists; 5 seconds on Utilities or 30 seconds inactive otherwise |
| Latency | Mandatory full scan before activation; every 10 seconds active; none Off. Native callbacks use fresh cache and named handles only. Pending inventory changes, expired/failed/racing/oversized scans fail closed. Renamed export-only injection has bounded periodic detection |
| Collections | Only visible pages build/resume metadata and check readiness; refresh on open/request; own observations every 2 seconds visible. Cache metadata, invalidate character observations on session/zone/transition changes |
| Identification/Inspector | Menu events and one queued click; no pending request means no actor lookup. Only selected/visible Inspector builds metadata and observes at 250 ms. Clear on dismissal, page close, target/session changes, stale actor and expiry; no nearby-player scanning |
| Activity | Observe only produced snapshots; reference guards and cached journal ordering; existing events/retention preserved |
| Profiles | Change notification, page open and explicit refresh; unsupported retry 5 seconds while open. No per-frame full-list IPC or hidden-page reads |

The Quest native service and its safety/selection policies remain byte-for-byte equivalent to the previously accepted source (namespace adapter only). Its existing dialogue/reward/confirmation/cutscene intervals remain unchanged. Supported provider protection is retained. No new game hook, background game-API thread or timer is added. The OS scan uses documented [K32EnumProcessModules](https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-enumprocessmodules) and brief [module reference lifetimes](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandleexw) with a reusable bounded buffer; exports are inspected, never invoked. Current API 15 fixtures compile the addon/IPC adapters.

Modern keeps Core 0.4.1, appearance, animations, resizing and input behavior. Closed windows do not render pages. Cache plugin-card content/actions, page names and ordered Activity snapshots; layout/rendering still has a cost while visible. Mount/Fashion metadata, filters, tracks and tokens are unchanged. Profiles keeps Core UI 0.3.1, existing apply/restart behavior and server-info display. No SRank/unrelated consumer change or metadata import.

## Profiles and measurement

The [owner contract](https://github.com/MarshalTitan/SentinelProfiles/blob/main/docs/HUB_IPC.md) projects only saved names/stable IDs, selected/last-applied, authoritative last-applied drift and busy state. Hub shows separate badges and clear missing/disabled/unsupported explanations. Open navigates/selects the existing editor; it never applies, edits rules or toggles plugins. Reject invalid/duplicate IDs, malformed payloads and unknown schemas. No names/IDs are persisted in Hub or diagnostics.

`/shub perf start` enables ten-minute RAM-only sampling. `/shub perf report` prints service counts, mean/worst duration, allocation bytes and counts above 2 ms, split by Hub window open/closed. `/shub perf stop` stops and reports. Inclusive timing includes called provider work; counts include cheap dormant guards. No automatic files; disabled scopes read neither clock nor GC counters. Reload clears measurements.

## Acceptance and release

Update Profiles to 0.2.1.8 first, then Hub to 0.7.1.0 through the existing custom repository. Keep only one enabled copy of each plugin and preserve configuration. The following are optional post-update observations.

1. With usual switches retained, profile walking/camera movement for 60 seconds with Hub closed and 30 seconds open. Compare with Hub disabled; report remaining stutter and `/shub perf stop` results.
2. Check dialogue responsiveness/manual choices, cutscene skip/dedicated confirmation and a choose-one reward. Off must stop actions. Check Anti-AFK and latency/provider conflict protection without running competing compensation engines together.
3. Inspect mount/accessory/supported loop emote; dismiss/change target/zone to clear. Open/refresh Mount/Fashion and verify own unlocks.
4. Verify all saved Profiles, selected/last-applied distinction and drift/busy. Open only selects/opens; apply only from Profiles and verify Hub updates. Disable/re-enable Profiles and verify clear status/recovery.
5. Reload Hub; saved switches/layout stay intact and session Activity clears, then records genuine changes.

Only observed cases become PASS. Live validation follows publication. Release Profiles first, then Hub through the existing immutable pipelines; verify tested/public ZIPs and exact child/central objects, preserve other catalog entries and reconcile canonical docs. Never replace a published asset. If a regression is reported, disable the affected plugin or feature while preparing a higher-version fix; do not delete settings or overwrite released assets.
