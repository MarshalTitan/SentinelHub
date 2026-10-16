# Sentinel Hub

Optional ecosystem control center for MarshalTitan Sentinel plugins. Open with `/shub`; export sanitized diagnostics with `/shub export`.

Hub is never required by SRankSentinel, PvPSentinel, SentinelHUD, ClassySentinel, SentinelProfiles, SentinelRelay or SentinelHunts. It does not enable plugins, apply profiles, move the player or execute gameplay actions.

## MVP

Dashboard, Plugins, Dependencies, Profiles, Utilities, Diagnostics, Appearance and About. Inventory comes from public Dalamud installed-plugin interfaces. Loaded means enabled in Dalamud, not gameplay-ready or automation-enabled. Only verified read-only boolean dependency IPC is called.

Consumer settings use a fixed source-verified command allowlist; some consumers toggle their windows. Disabled/missing/restricted components have unavailable actions. No reflection, private configuration reads, consumer assembly references or required Hub service.

Profiles selected/last-applied/drift status, Relay connectivity, consumer faults and update availability are **unknown/not queried** in 0.1. Consumers remain authoritative. Utilities are explicit inactive plans; no HUD migration is part of this build.

## Dependencies and build

Dalamud API 15; bundled Core/UI NuGet **0.4.1**, tag **v0.4.1.0**, source **e6527400680cddc39803321792b7c3b02fb0501d**. The latest approved Modern API is unchanged from 0.3.1. Exact NuGet lock and SHA256 bootstrap verification are committed; ZIP validation compares bundled DLL bytes with those packages.

Cloud CI restores locked packages, runs policy tests and real native ImGui rendering tests, builds the installable ZIP and checks its contents. See [architecture](docs/ARCHITECTURE.md) and [acceptance](docs/ACCEPTANCE.md).

Publication is a main push containing `[publish-live]` after CI passes. Assets are immutable; source/tag/version and public ZIP are verified before first child manifest creation and shared distribution. Central registration follows the first valid release; child workflows never write central repo.json.
