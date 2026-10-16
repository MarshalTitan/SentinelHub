# Sentinel Hub

Optional ecosystem control center for MarshalTitan Sentinel plugins. Open with `/shub`; export sanitized diagnostics with `/shub export`.

Hub is never required by SRankSentinel, PvPSentinel, SentinelHUD, ClassySentinel, SentinelProfiles or SentinelRelay. It does not enable plugins, apply profiles or move the player. The retired Sentinel Hunts test component is no longer listed in Hub.

## Control center and utilities

Dashboard, Plugins, Dependencies, Profiles, Utilities, Diagnostics, Appearance and About. Inventory comes from public Dalamud installed-plugin interfaces. Loaded means enabled in Dalamud, not gameplay-ready or automation-enabled. Verified read-only dependency IPC covers vnavmesh, Lifestream, RotationSolverReborn and Splatoon.

Consumer settings use a fixed source-verified command allowlist; some consumers toggle their windows. Disabled/missing/restricted components have unavailable actions. No reflection, private configuration reads or consumer assembly references.

Profiles selected/last-applied/drift status, Relay connectivity, consumer faults and update availability remain **unknown/not queried**. Consumers remain authoritative.

**0.2.0.0 adds default-OFF Anti-AFK.** Hub uses the existing HUD timer behavior: one synchronous check every 10 seconds, resetting finite inactivity timers above 30 seconds. It generates no input, movement or chat. Compatible loaded HUD 0.8.4.6+ must acknowledge ownership before Hub writes; installed legacy/unknown HUD blocks Hub. Optional versioned primitive IPC prevents duplicate owners and preserves HUD's original preference. Import copies that single preference only on explicit click. Disabling/unloading Hub returns control to HUD.

**0.3.0.0 adds default-OFF Quest Convenience**, with explicit import, preserved ordinary-dialogue/manual-choice behavior, contextual cutscene skipping and reward selection. Compatible loaded HUD 0.8.4.7+ must acknowledge ownership. HUD retains its original quest preferences/engine for rollback. See [Quest acceptance](docs/QUEST_CONVENIENCE_MIGRATION.md).

Latency/Weaving remains inactive. HUD utility cleanup is separate from replacement proving. See [migration acceptance and rollback](docs/ANTI_AFK_MIGRATION.md).

## Dependencies and build

Dalamud API 15; bundled Core/UI NuGet **0.4.1**, tag **v0.4.1.0**, source **e6527400680cddc39803321792b7c3b02fb0501d**. The approved Modern API is unchanged from 0.3.1. Exact NuGet lock and SHA256 bootstrap verification are committed; ZIP validation compares bundled DLL bytes with those packages.

Cloud CI restores locked packages, runs policy tests and native ImGui rendering tests, builds the installable ZIP and checks its contents. See [architecture](docs/ARCHITECTURE.md) and [validation](docs/VALIDATION.md).

Publication is a main push containing `[publish-live]` after CI passes. Assets are immutable; source/tag/version and public ZIP are verified before shared distribution. Child workflows never write central repo.json.
