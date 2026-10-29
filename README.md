# Sentinel Hub

Optional ecosystem control center for MarshalTitan Sentinel plugins. Open with `/shub`; export sanitized diagnostics with `/shub export`.

## Treasure Maps - 0.8.1.1

Adds an optional Treasure Maps page and independent floating queue: deciphered-map location detection, Party flags (Alliance opt-in), exact saved map variants, Open/Next, manual flag sharing, Complete with brief Undo, Delete/reorder and character-scoped unfinished queues. Enable the module in Treasure Maps; upgrades leave it Off. Share restores the actual flag and copies `/p <flag>` for manual sending. No teleport/navigation, external service or additional plugin is needed. See [implementation, performance, API/license audit and live checks](docs/TREASURE_MAPS.md). Native detection and real chat/map behavior remain pending supervised validation; automated tests do not certify them.

HUD visuals and camera, SRankSentinel, PvPSentinel, ClassySentinel, SentinelProfiles and SentinelRelay work independently of Hub. Anti-AFK and Quest Convenience are owned by Hub after HUD 0.8.5.0. It does not enable plugins, apply profiles or move the player. The retired Sentinel Hunts test component is no longer listed in Hub.

0.8.0.1 corrects the native treasure-display binding: the first release matched an unrelated UI helper, so deciphering did not identify or open the location. The corrected call and handler ABI were checked offline against game build 2026.10.16.0000.0000. Existing queues, settings and dormant performance behavior are preserved.

0.8.0.2 makes the floating queue's title-bar X close and remember the window state. Hub returns to Show Floating Queue, and the same button can reopen it. Queue contents and saved window geometry are preserved.

0.8.1.0 gives both queue views the same per-entry controls, visible Up/Down arrows and readable native More menus. The floating queue shows every unfinished entry in its scroll area. Reordering uses the existing shared, persisted queue; Next Map follows the first entry. Complete/Undo, decipher detection, sharing, X close and performance scheduling are preserved.

## Control center and utilities

Dashboard, Plugins, Health, Profiles, Utilities, Mount Collection, Fashion Accessories, Player Inspector, Recent Activity, Diagnostics, Appearance and About. Inventory comes from public Dalamud installed-plugin interfaces. Loaded means enabled in Dalamud, not gameplay-ready or automation-enabled. Verified read-only dependency IPC covers vnavmesh, Lifestream, RotationSolverReborn and Splatoon.

Consumer settings use a fixed source-verified command allowlist; some consumers toggle their windows. Disabled/missing/restricted components have unavailable actions. No consumer assembly dependency or private reflection.

Profiles selected/last-applied/drift and Relay runtime connectivity remain **unknown unless a compatible optional provider responds**. Public versions are a dated explicit catalog fetch in Hub 0.7. Consumers remain authoritative.

**0.2.0.0 adds default-OFF Anti-AFK.** Hub uses the existing HUD timer behavior: one synchronous check every 10 seconds, resetting finite inactivity timers above 30 seconds. It generates no input, movement or chat. Compatible loaded HUD 0.8.4.6+ must acknowledge ownership before Hub writes; installed legacy/unknown HUD blocks Hub. Optional versioned primitive IPC prevents duplicate owners and preserves HUD's original preference. Import copies that single preference only on explicit click. HUD 0.8.4.6/0.8.4.7 can resume their saved preferences when Hub stops; HUD 0.8.5.0 has no fallback engine and leaves actions off.

**0.3.0.0 adds default-OFF Quest Convenience**, with explicit import, preserved ordinary-dialogue/manual-choice behavior, contextual cutscene skipping and reward selection. Compatible loaded HUD 0.8.4.7+ must acknowledge ownership. HUD retains its original quest preferences. Its fallback engine was removed in 0.8.5.0 after scoped replacement acceptance. See [Quest acceptance](docs/QUEST_CONVENIENCE_MIGRATION.md).

**0.3.1.0** clarifies compatibility with HUD **0.8.5.0**: disabling Hub turns these utility actions off. Update Hub first, then HUD. Existing Hub opt-ins and native algorithms are unchanged. See [cleanup acceptance and rollback](docs/UTILITY_CLEANUP.md). Latency/Weaving follows as the default-OFF 0.4.0.0 module below.

## Live and development installations — 0.3.1.1

Dashboard and Plugins prefer the non-development installation when Dalamud exposes both copies of the same plugin, even if the live copy is disabled. Version and loaded/restricted state come from that same copy; version uses Dalamud's effective installed version. Open settings checks that chosen copy again. A development-only installation is labeled explicitly. Versions are not pinned to catalog values.

The user reported PASS for the Hub 0.3.1.0 / HUD 0.8.5.1 cleanup checklist, with this separate SRank inventory correction requested. Utility runtime/settings, HUD and SRank gameplay are unchanged. [Focused check](docs/LIVE_DEV_DISCOVERY.md).

## Latency / Weaving — 0.4.0.0

Default **Off**; optional **Measure only** and **Compensate** modes. Independently implemented, bounded correction of own instant-action response delay with a fixed 40 ms buffer and 250 ms maximum correction. Typed hooks forward original calls exactly once. Cast-time actions/long locks, packets, cooldowns and action selection are unchanged.

Known loaded NoClippy and XivAlexander runtime/loader detection blocks this module, including identifying exports in renamed proxies. Unknown native checks pause it. Off, logout, death, transition, mode/territory change or unload clears pending work and releases hooks. Native setup/contention faults require Off before retry. Only typed state and bounded numerical measurements are exported; no action/actor IDs, module paths or packet text.

[In-game proving procedure, limits and rollback](docs/LATENCY_WEAVING.md). Actual game-hook correctness and felt weaving improvement remain unverified until that gate passes. Other NoClippy features such as status prediction are outside this module.

## Dependencies and build

Dalamud API 15; bundled Core/UI NuGet **0.4.1**, tag **v0.4.1.0**, source **e6527400680cddc39803321792b7c3b02fb0501d**. The approved Modern API is unchanged from 0.3.1. Exact NuGet lock and SHA256 bootstrap verification are committed; ZIP validation compares bundled DLL bytes with those packages.

Cloud CI restores locked packages, runs policy tests and native ImGui rendering tests, builds the installable ZIP and checks its contents. See [architecture](docs/ARCHITECTURE.md) and [validation](docs/VALIDATION.md).

Publication is a main push containing `[publish-live]` after CI passes. Assets are immutable; source/tag/version and public ZIP are verified before shared distribution. Child workflows never write central repo.json.

## Mount Collection — 0.5.1.0

Independent passive local-game database with search/sort, current-character unlocks, names/icons/seats/actions/BGM, an attributed 356-mount acquisition snapshot, gil-shop prices/requirements, detailed exchange costs, official source/store links, market eligibility and six independently authored expansion trial/meta tracks. Release expansions use matched community introduction patches, with coverage/mismatched identities shown. Category filters distinguish Online Store, other paid/promotional sources and in-game methods. Current availability stays separately unverified; historical event text is retained. Totem progress uses simple verified exchanges and four loaded main bags only. Player context-menu lookup is an explicit default-OFF option; no target-info/tooltip hook or game actions.

[Data audit and provenance](docs/MOUNT_COLLECTION_METADATA.md) · [Acceptance and rollback](docs/MOUNT_COLLECTION.md). WhichMount was a behavioral README reference only; no code/CSV/resource dataset was copied. Existing utilities and Core pins are unchanged.

## Fashion Accessories — 0.6.0.0

Independent passive ornament catalog and own unlocks, with source/patch metadata, source/expansion/market/store/availability filters, explicit unknowns and retired Facewear handling. One opted-in player-identification action opens Mount or Fashion details; the current typed actor field is read on the framework thread without a hook. No API calls during collection browsing or character data persistence.

[Data/API/rights audit](docs/FASHION_ACCESSORIES_AUDIT.md) · [Supervised acceptance](docs/FASHION_ACCESSORIES.md). Current availability has separately dated evidence and remains Unknown for most entries. Existing utility/Core behavior and Mount preference remain intact.


## Coordinated operations — 0.7.0.0

This release adds Health/Dependency Doctor, sanitized diagnostics ZIPs, compatibility self-protection, Player Inspector with supported explicit loop-emote observation, and bounded Recent Activity. Barding, settings backup/restore and global pause were removed following supervised feedback. Existing Mount/Fashion page layouts/data and saved utility preferences remain intact. Core/UI stay 0.4.1.

The attributed identity-gated acquisition snapshot covers 201 unlockable emotes under the audited FFXIV Collect API reuse permission. Current availability stays Unknown. Emote identification requires matching live loop state, never arbitrary animations/poses. Plugins actions move right at roomy widths and stack below when narrow; the window remains resizable. Current HUD no longer needs utility handoff acknowledgments because it contains no native utility engines. Anti-AFK keeps lightweight 10-second timer checks, with ownership polling reduced to once per second.

See [audit and source boundaries](docs/OPERATIONS_EXPANSION.md), [supervised acceptance](docs/OPERATIONS_ACCEPTANCE.md) and [release PR](https://github.com/MarshalTitan/SentinelHub/pull/15). The focused native release gate passed on 2026-10-18 at 16:13 America/Toronto; publication and exact catalog reconciliation are tracked in the release PR.

Anti-AFK is kept to a heading and state-labeled switch, with no text/counter below it. Activity and live diagnostics are RAM-only, clear on Hub reload/game close and do not create automatic history files. Startup retires only the earlier candidate's exact recent-activity.json. Optional manual export atomically replaces one diagnostics/sentinel-diagnostics.zip; exported files intentionally survive game close. Earlier manually saved timestamp ZIPs are not deleted. Saved user preferences remain on disk normally. See the latest [scoped results and focused checks](docs/OPERATIONS_ACCEPTANCE.md).

Plugin cards on Dashboard, Plugins and Profiles now measure the real inner wrap width and omit trailing row gaps/extra height allowance. Standard Core card padding, responsive action placement and resize behavior are retained. Native regression checks bound unused bottom space and reject clipping in both themes at 1×/1.5×/2× scale, including narrow→wide→narrow resizing, action dispatch/disabled gating and the Profiles status card. This is layout-only; compact Anti-AFK and session-only activity/manual-export changes remain included.

## Published 0.7.0.0

[Immutable release](https://github.com/MarshalTitan/SentinelHub/releases/tag/v0.7.0.0) published from `8e449cd4e75001a277ffdce2ca45e725801f1293`. [Release pipeline](https://github.com/MarshalTitan/SentinelHub/actions/runs/39162571980) validates the package and compares the downloaded public ZIP against its tested CI artifact before distribution. GitHub asset metadata: SentinelHub.zip, 429065 bytes, SHA256 `7a7ded95f95e5d7fc581f7d64ca4736518c88c6e22fd7f6dfe26e0edc6e8367b` (metadata read; hash equality is checked by the pipeline). Exact child/central promotion evidence is recorded in the canonical Sentinel RELEASE_PROCESS.md and release PR.

For normal use, disable the isolated development copy, enable/update live Sentinel Hub through the existing custom repository and confirm 0.7.0.0. Saved preferences remain intact. The focused gate is closed; unreported scenarios retain their documented scope.


## 0.7.1.0 performance and Profiles

Shared inventory, dormant inactive modules, addon-driven Quest with bounded fallback, cached latency conflict checks and an authoritative Profiles overview are included in this live update. Requires Profiles 0.2.1.8 for the new overview; older builds show a clear compatibility message. Movement/camera and interaction validation remains pending. See [performance evidence and checklist](docs/PERFORMANCE_PROFILES.md).

0.8.1.1 adds icons beside More menu actions and draggable owner/location links in both queue views. Drop above or below another map to reorder, including between the two windows; the placement line previews the destination. Clicking a link opens its flag. Up/Down controls remain available. Saved queue order, Next Map, Complete/Undo and dormant processing are preserved.
