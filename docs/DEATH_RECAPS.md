# Death Recaps — Hub 0.9.0.0

Death Recaps adds a Core Modern 2 page, local history and optional independent window. Existing Treasure Maps and performance fixes are unchanged. Core/UI stay 0.4.1; HUD is optional; no new dependency, telemetry, upload or external service is added.

## Evidence

The default records local HP, maximum HP and shield percentage every 125 ms inside duties/PvP, and status IDs/stacks at most once a second while alive plus on death. Only changes produce events. An alive-to-zero HP transition records death, with approximately 125 ms observation precision. Loading while already dead does not invent a death. Optional PvE party capture, off by default, uses at most eight observable party actors, without an enemy/object-table sweep. Unobservable members are not invented.

Each actor retains at most 512 events over 60 seconds, with 10/30/60-second review windows. A sampled HP change does not prove attributable damage/healing. Shield percentage is not exact barrier HP. Buff/debuff snapshots are context; no mitigation percentages are inferred. Event buffer overflow is reported.

**Experimental incoming action details** is a separate opt-in, off by default. It uses the current FFXIVClientStructs ActionEffectHandler.Receive binding through Dalamud's hook API, validates text/module bounds and pristine/live entry bytes, and refuses existing detours. It pauses when BetterDeaths/DeathRecap is loaded. Failed binding leaves base recaps usable. There is no repeated signature scan. Primitive data for watched targets enters a 1,024-event handoff; all object/name/data resolution runs on Framework. Vanished/unobservable sources are Unknown. Global-sequence/source/target/slot duplicates are ignored. Source-side reflected effects, periodic ticks and unsupported action types are excluded. Amounts are observed action-effect amounts, not verified HP loss/effective healing.

**Confirmed fatal blow, exact pre-hit HP, exact shield HP and overkill remain Unknown/Unavailable.** This release does not correlate EffectResult/actor-control death evidence, derive a killer from the last hit, or derive damage from targeting. The pure overkill policy requires exact inputs; the live adapter never supplies estimates. Optional native capture requires in-game validation and must not be represented as game-validated by CI.

## Frontline / HUD

HUD 0.8.9.0 owns live classification and the Targeting Me Counter. Its existing 125 ms scan feeds a compressed 30-second history and seals the last sample before the death clear. Hub reads a versioned **completed-deaths-only** JSON contract and never scans enemies for targeting history. Hub waits up to one second for same-territory/local-entity/time matching. Unavailable, disabled, incompatible or unloading HUD yields targeters Unavailable; the next death can recover after reload. Old sessions and distant timestamps cannot match.

PvP recaps show final job counts, maximum simultaneous targeters, sample freshness, 15/30-second observed coverage and approximate per-enemy focus intervals. A final sample older than one second is labeled stale. **Observed Targeters** are separate from **Confirmed Damage Sources (captured action effects)**. Hard targeting does not establish an attack, all damage contributors or a killer. No nearby/hidden enemy inference, live rankings, identity actions, strategy integration, targeting, navigation, movement or combat action is provided.

Contract: https://github.com/MarshalTitan/SentinelHUD/blob/main/docs/THREAT_DEATHS.md . Official Dalamud repository restrictions prohibit PvP plugins; this uses Sentinel's existing custom repository and does not claim official approval: https://dalamud.dev/plugin-publishing/restrictions/ .

## History and popup

Default history is 30 deaths per character, adjustable 5–100, under Hub's local configuration directory in death-recaps. Changes are coalesced, normal writes deferred until out of combat, then immutable JSON is atomically written by one worker with no game APIs. Logout/unload flush pending records. Files are bounded to 8 MB; corrupt input is rejected. Buffers reset on respawn/territory/session/unload; detected pending deaths survive reset. Delete removes a record; Clear requires confirmation.

UI player names are hidden by default. Disk player names are independently **redacted by default**; jobs, NPC/action names and opaque per-life enemy tokens remain. Retaining names requires explicit opt-in. The transient local entity ID used for matching is always removed before persistence. Setting redaction rewrites history after combat; discarded names cannot be recovered. No permanent full combat log is stored.

Popup modes: Off, Both, PvE only, PvP only; persist until dismissal or expire after 30 seconds. The resizable Modern 2/Classic window respects reduced motion and avoids taking focus. Open full recap, Dismiss and X are available. Closed windows do not read/draw recap state; Dalamud remembers geometry. Synthetic PvE/PvP previews are visibly labeled and never saved.

## Automated coverage

Pure policies cover death/respawn/initial-dead transitions, bounded chronological timelines, HP/shield/status preservation, missing fatal evidence and exact-input overkill, amount extension, job aggregation, maxima, freshness/boundary handling, targeter/damage separation, IPC validation, persistence/retention/delete/corrupt recovery, privacy and synthetic exclusion.

Native packet-memory fixtures exercise the current structure, damage/healing/high bits, source-side/unwatched/type rejection and bounded handoff. HUD fixtures cover change compression, final pre-clear snapshot, repeated deaths, respawn/territory/session reset, opaque identities and warmed 10,000 unchanged samples with zero allocations in the added history layer. Hub fixtures cover unavailable/reload/version/session IPC, unsubscribe/disposal, preview isolation and 10,000 dormant ticks with zero allocations/game-service reads. Native ImGui adds 32 popup/full-page theme/scale/width/PvE/PvP scenarios with X, modes, scrolling, balanced stacks, unchanged settings and closed dormancy. Existing Hub/Treasure/inspection/utility regressions remain mandatory.

`/shub perf start` then `/shub perf stop` includes DeathRecaps separately. Runner timings and fixtures are not game movement or native capture evidence.

## Supervised game acceptance — pending, not claimed complete

1. In a PvE duty, verify observed death time, HP/shield/status order in each review window, environmental/unknown deaths, rapid respawn and repeat. Exercise optional party recording with visible/unobservable members.
2. With optional details enabled, compare incoming single/multi-target attacks, blocked/parried damage, healing and shields against visible combat logs. Check vanished casters, duplicate effects, exclusions and hook conflict/unload/reload. Do not enable details by default without evidence. Fatal attribution remains Unknown.
3. In Frontline with HUD/counter enabled, compare visible pre-death jobs/counts, final freshness, maxima and focus changes. Confirm targeters never become assumed attackers. Repeat deaths, respawn, zone, disable/reload both plugins and verify no stale association.
4. Verify restart/retention/redaction/delete/clear; popup filters, X/dismiss, scales/narrow sizes; saved records remain after respawn.
5. Compare movement/camera performance with feature Off, enabled outside duty, active in duty, and optional party/details. Verify no added stutter, continuous combat writes, duplicate enemy scans or leaked hooks/subscriptions.

## References

Better Deaths at b3ec13eec674dff989ba53f85786fc33e3e6fbe5 (MIT, Nainaiowo 2026): https://github.com/Nainaiowo/better-deaths . Reviewed capture boundaries, action/result handling, chronological UI, redaction and popup. This is a smaller independent implementation without its data catalogs, replay, estimation engine or plugin dependency. Attribution is in THIRD_PARTY_NOTICES.md. Primary binding: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/Character/ActionEffectHandler.cs .
