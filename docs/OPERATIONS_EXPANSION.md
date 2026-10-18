# Hub operations candidate — audit and boundaries

Candidate **0.7.0.0**, [Hub PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15). Live baseline remains **0.6.0.0** at `9aec4202373bee5e3b10adda35287987c6340b38`. One combined native gate is required before public release. The latest successful artifact in the PR supersedes earlier candidates.

## Scope after supervised feedback

Keep Health/Dependency Doctor, sanitized Diagnostics, Compatibility, Player Inspector (Mount/Fashion/supported emotes), Recent Activity, and existing utilities/collections. Remove chocobo barding inspection, settings backup/restore and global automation pause completely, including their UI, services, IPC, bundled barding metadata and tests. The game already provides chocobo equipment inspection. Individual plugins and Hub utility switches remain authoritative. No consumer/Core update is required.

Retained page IDs and window identity stay stable. The removed candidate Backups page ID normalizes to Dashboard; an old candidate pause property is ignored rather than changing saved utility switches. Explicit activity-code values preserve optional API identities; retired codes remain invalid. The exact recent-activity.json history file from the earlier candidate is removed on startup. User settings, manual exports and other plugin files are not deleted. The user reported Quest Convenience Off persisted from dev 0.7 to live 0.6; this confirms preference persistence only, not all native utility behavior.

## Live owner/API audit

| Owner | Audited source | Version / config schema |
|---|---|---|
| Hub | 9aec4202373bee5e3b10adda35287987c6340b38 | live 0.6.0.0 / 1 |
| SRank | e2ea4cbec8f8a1355bafc5690b58a3f4588e2255 | 0.7.62.0 / 16 |
| PvP | 0971ecdc53f5b4b5582295aab1748742164ae522 | 0.3.1.23 / 19 |
| HUD | f6aa823401a11c0540b4893fde83eed2b247e3d2 | 0.8.5.2 / 12 |
| Profiles | f6288d82a612eac4d16d0963384bee12104d2f33 | 0.2.1.7 / 3 |
| Classy | 694870aa945b6ece95cf0b4343fc428eeb521853 | 0.8.6.0 / 5 |
| Relay | 64544ad9bcc386d6f92a05a963c07625f99604d8 | 0.5.0.7 / 5 |
| Core | e6527400680cddc39803321792b7c3b02fb0501d | Hub bundled Core/UI 0.4.1 |

Separate policies/services/panels own health, diagnostics, compatibility, inspection and activity. MainWindow composes them using Core's existing shell/cards. Plugins and Dashboard cards put measured-width actions on the right when room remains for readable information, and stack them below at narrow widths. The same top-level window remains resizable; there is no fixed wide mode. Native UI tests resize one window narrow → wide → narrow in both themes/scales, checking height reduction, clipping and preferences. Mount/Fashion page source, metadata, tracks and token logic remain unchanged.

Health distinguishes installed/live inventory, the actually loaded development copy, public catalog version, internal enable/readiness and IPC response. Public versions require an explicit bounded fixed-URL fetch. Failure becomes Unknown; a loaded DLL alone is never Ready. vnavmesh readiness, Lifestream/RSR/Splatoon responses and unknown HuntAlerts/Sonar feed state remain distinct. Direct Faloop is dormant in audited SRank 0.7.62.0. The existing own-character-scoped SRank Remote.v1.Status endpoint is queried read-only; only loaded/enabled/ready, bounded queue count and recovering primitives survive projection. Names, character keys, hunts and status strings are discarded. Unsupported consumer endpoints remain unavailable, without requiring those consumers to update.

Diagnostics creates a ZIP with report.json, hub-state.json and summary.txt, reconstructed from allowlisted identities, numeric versions, enums, bounded counters and public territory/world rows. Typed health, compatibility and recent-event observations are included. Config documents, chat, credentials/ciphertext, paths, exceptions, names, content/account/entity IDs and inspection identities are excluded. Known-secret canaries exercise provider, health and event boundaries. Export reads game/IPC observations on the framework thread. Live faults and activity remain in RAM and clear on Hub unload/game close. Only an explicit Export action writes diagnostics/sentinel-diagnostics.zip, atomically replacing the previous export; no automatic export or timestamp-file accumulation. The chosen export remains on disk after closing the game. Older manually exported ZIPs, if any, are not deleted.

Compatibility reads reviewed TextAdvance enabled/paused/feature IPC. Definite overlap suspends only affected Hub behavior. Unavailable known-provider state is a labeled conservative suspension. Loaded NoClippy is possible overlap because its module activity is not exposed; existing native XivAlexander markers block compensation. YesAlready and multipurpose tweak plugins remain possible/informational where feature state is unverified. Hub never changes another plugin's settings. Effective options preserve saved preferences and recover when overlap clears.

## Utility ownership and Anti-AFK timing

HUD **0.8.5.0+** removed its native Anti-AFK/Quest engines. Current [AntiAfkHubBridge](https://github.com/MarshalTitan/SentinelHUD/blob/f6aa823401a11c0540b4893fde83eed2b247e3d2/src/SentinelHUD/Services/AntiAfkHubBridge.cs) and [QuestConvenienceHubBridge](https://github.com/MarshalTitan/SentinelHUD/blob/f6aa823401a11c0540b4893fde83eed2b247e3d2/src/SentinelHUD/Services/QuestConvenienceHubBridge.cs) retain compatibility/preferences but perform no game work. See [HUD cleanup](https://github.com/MarshalTitan/SentinelHUD/blob/f6aa823401a11c0540b4893fde83eed2b247e3d2/docs/UTILITY_CLEANUP.md).

Hub therefore needs no HUD lease/IPC acknowledgment on these engine-free versions. It also works with HUD absent. This fixes the unnecessary wait seen when an isolated development Hub is loaded beside a disabled live installation: the old HUD bridge can discover the disabled live entry first. The fix bypasses obsolete coordination, rather than hiding a blocked status. Older/unknown HUD builds retain the existing minimum-version and exclusive-handoff guards because they may contain a native writer. Their explicit conflict/update message remains relevant.

Keep the native **10-second** check: three direct InputTimerModule counter reads with conditional resets above 30 seconds, no network queries, key/movement/chat synthesis or background worker. Extending it to five minutes would change inactivity behavior for negligible absolute work saved. Anti-AFK ownership/discovery checks are now bounded to once per second rather than every frame; Off/login changes bypass that delay. Native timer code and the accepted Quest dialogue/cutscene/reward engine are unchanged. Latency readiness/conflict suspension remains independent of the removed global pause.

## Current game/API verification

FFXIVClientStructs (MIT) provides typed Character, EmoteController and GameObject declarations; Dalamud API 15 provides object/context-menu validity, framework checks and IUnlockState.IsEmoteUnlocked. Product/owned-memory fixtures bind FFXIVClientStructs **7.57.0.9386**. Public API declarations/semantics are interoperability references; no AGPL implementation is incorporated.

One `Sentinel Hub: Inspect player` subscription preserves Mount/Fashion opt-ins and adds the default-OFF Inspector. Menu callbacks retain numeric tickets, never actor references/pointers/names. Framework processing re-fetches identity/session/territory, rejects old clicks and invalid/loading/transformed actors, and clears old results before a new request. Inspector observations clear on close/off/logout/zone/despawn/identity reuse or five-minute expiry. Opening closed/minimized Hub is evaluated before visibility checks. Mount/Fashion-only mode keeps the accepted collection workflow.

Emote recognition requires explicit EmoteController.EmoteId, a usable local Emote/TextCommand entry, CharacterModes.EmoteLoop and matching Emote.EmoteMode / actor ModeParam. Idle, pose/InPositionLoop, movement, combat and action-timeline IDs are never substituted. Short one-shot emotes deliberately remain unidentified. Supported loops refresh to None when ended. No playback, new unmanaged hook, target-tooltip modification, manual offset or external character query is added. WhichMount and non-permissive public projects remain behavior/reference only; Sentinel owns matching/lifetime policies and tests.

## Acquisition source rights and provenance

The proposal was reported in chat before the initial metadata import. FFXIV Collect's creator explicitly invites API data use in other projects: [developer invitation](https://ffxivcollect.com/), [creator statement](https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/), [API documentation](https://ffxivcollect.com/api/docs). This permission is distinct from its software's MIT license. No wiki/unlicensed/copyleft dataset, artwork, descriptions, user statistics/profiles or market prices are imported. UI text, embedded headers and THIRD_PARTY_NOTICES.md retain attribution.

The retained `Inspection/Data/Emotes.json` contains **201 unlockable emotes**, exact ID/English-identity matched before import. Endpoint: https://ffxivcollect.com/api/emotes?language=en . Retrieved `2026-10-18T16:58:19.147093+00:00`; original response SHA256 `b7d257687963a98d8d910329b2a06ed790d4adadd4d794578943f3c4103dd153`. Initial public game cross-check version `5c744a8d8daafb25`, Emote response SHA256 `4d25d916eec3ea5430e25b95a00ecf803075290d8874488d5dd6cb7b0e46921f`; [initial audit](https://github.com/MarshalTitan/SentinelHub/actions/runs/39147407586). The initially researched 107-row barding snapshot has been removed from the product and ongoing audit.

Keep only ID, English identity, source type/text and introduction patch. Runtime requires exact numeric ID plus case-insensitive English game identity; missing/drifting metadata stays Unknown. This is not every built-in/variant emote. Local action 2633 maps unlock items via Emote.UnlockLink; explicit quest/achievement/gil-shop links add source evidence. Shop presence does not prove access. Current obtainability remains **Unknown**; events/promotions may be historical and recorded Online Store is not verified regional stock. JSON corrections require provenance review and a tested release. The audit workflow produces review artifacts, never automatic imports or raw bundled game tables. Existing Mount/Fashion metadata is unchanged.

## Activity and proving

`SentinelHub.Activity.Publish.v1(string source, int code) -> bool` accepts loaded known Sentinel sources and allowlisted typed codes on the framework thread, with no arbitrary message. Reports are advisory. Optional health int and diagnostics JSON contracts use strict projection. Unsupported Profiles/Relay/SRank gameplay events are not invented from private state/config files.

Activity retains at most 200 typed events in RAM for the current Hub session (at most seven days for a continuously running session), groups source/code repeats within a minute and caps counts. It never loads or writes an activity history. A new Hub service starts empty. Invalid/unknown/retired event codes are rejected. Health/compatibility transitions and diagnostics results provide actual events. Atomic-storage failure tests verify original bytes are retained and temporary files removed.

Policy tests, real native ImGui regressions, owned-memory reader fixtures and installable-package validation are required. They do not prove live game replication or utility operation. Use [the supervised checklist](OPERATIONS_ACCEPTANCE.md). Rollback: turn utility preferences Off, unload the isolated candidate and re-enable live 0.6.0.0. Retain user configuration; no consumer restart/config restore is performed.

## Latest supervised observations and storage simplification

2026-10-18 15:40 America/Toronto: the user reports Anti-AFK's counter reached 46 and stopped increasing after Off. The attached screenshot shows Active with a count of 47, consistent with an additional check before capture. Record PASS for running checks and Off stopping them, not an independent guarantee of network stability or every idle interval. Inspector Off removed its context action; On resolved an accessory and emote. Record those scoped PASS results without inferring separately verified names/icons/own unlock/source or stale-target cases. Recent Activity visibly records changes; session clearing is the new implementation still to verify after reload.

Anti-AFK now displays only its heading and one state-labeled switch. Active/Off/Starting/Waiting/Blocked/Unavailable remain distinguishable within the control; no explanation, cadence or counter text appears underneath. The native engine is unchanged. Diagnostics remains optional troubleshooting, not automatic logging. Unit coverage exercises session restart/clear, exact legacy-file cleanup without touching settings/exports, repeated manual export replacing one file, and atomic-write failure. Existing read-only metadata/collection behavior is unchanged.

Plugin cards on Dashboard, Plugins and Profiles now measure the real inner wrap width and omit trailing row gaps/extra height allowance. Standard Core card padding, responsive action placement and resize behavior are retained. Native regression checks bound unused bottom space and reject clipping in both themes at 1×/1.5×/2× scale, including narrow→wide→narrow resizing, action dispatch/disabled gating and the Profiles status card. This is layout-only; compact Anti-AFK and session-only activity/manual-export changes remain included.
