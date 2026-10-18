# Sentinel Hub

Optional ecosystem control center for MarshalTitan Sentinel plugins. Open with `/shub`; export sanitized diagnostics with `/shub export`.

HUD visuals and camera, SRankSentinel, PvPSentinel, ClassySentinel, SentinelProfiles and SentinelRelay work independently of Hub. Anti-AFK and Quest Convenience are owned by Hub after HUD 0.8.5.0. It does not enable plugins, apply profiles or move the player. The retired Sentinel Hunts test component is no longer listed in Hub.

## Control center and utilities

Dashboard, Plugins, Health, Profiles, Utilities, Mount Collection, Fashion Accessories, Player Inspector, Recent Activity, Backups, Diagnostics, Appearance and About (new operations pages are part of the held 0.7 candidate). Inventory comes from public Dalamud installed-plugin interfaces. Loaded means enabled in Dalamud, not gameplay-ready or automation-enabled. Verified read-only dependency IPC covers vnavmesh, Lifestream, RotationSolverReborn and Splatoon.

Consumer settings use a fixed source-verified command allowlist; some consumers toggle their windows. Disabled/missing/restricted components have unavailable actions. No consumer assembly dependency or private reflection. The held backup module reads only explicit known config files on user request and projects reviewed fields.

Profiles selected/last-applied/drift and Relay runtime connectivity remain **unknown unless a compatible optional provider responds**. Public versions are a dated explicit catalog fetch in the held candidate. Consumers remain authoritative.

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


## Coordinated operations candidate — 0.7.0.0 (not published)

One held integration train adds Health/Dependency Doctor, sanitized diagnostics ZIPs, compatibility self-protection, Player Inspector with exact supported emote/barding observation, cooperative pause acknowledgements, bounded Recent Activity and selective schema-aware backups. Mount/Fashion page layouts and existing saved utility preferences remain intact. Core/UI stay 0.4.1.

The new acquisition snapshot covers 107 bardings and 201 unlockable emotes under the already audited FFXIV Collect API reuse permission, gated by exact game identity. Availability remains Unknown. Barding uses current typed owned-actor model fields and rejects ambiguous matches; emote identification supports explicit matching loop state, never arbitrary animations/poses.

Pause stops Hub's own writers. Current public SRank/PvP builds lack the optional pause contract and are explicitly **Unsupported / may still automate**, so use their own controls. Backups are reviewed settings subsets: Profiles/Classy/Relay are presentation-only; credentials, names/IDs, histories and unreviewed fields are excluded. Restore handles one owner atomically with a pre-restore backup, keeps Hub paused and rejects loaded external owners/newer or unreviewed formats.

[Audit/architecture/rights/limits](docs/OPERATIONS_EXPANSION.md) · [One combined proving checklist and rollback](docs/OPERATIONS_ACCEPTANCE.md). [Draft PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15) stays unmerged until the required supervised gate passes. Live public Hub/catalog remain 0.6.0.0; no intermediate public releases.
