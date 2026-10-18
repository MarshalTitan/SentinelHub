# Coordinated Hub operations expansion — research and delivery plan

Status: integration branch, not published. Live baseline Hub 0.6.0.0 at 9aec4202373bee5e3b10adda35287987c6340b38. Candidate version 0.7.0.0. One consolidated public release follows the combined in-game gate; individual phase commits do not publish.

## Audited live owners

| Owner | Current source | Config schema / boundaries |
|---|---|---|
| Hub | 9aec4202373bee5e3b10adda35287987c6340b38 | 1; typed preferences, no credentials; existing collection pages retained |
| SRank | e2ea4cbec8f8a1355bafc5690b58a3f4588e2255 | 16; credentials/session, pending alerts and history must not enter backup/diagnostics |
| PvP | 0971ecdc53f5b4b5582295aab1748742164ae522 | 19; numeric/boolean policy preferences; automation and internal provider state are distinct |
| HUD | f6aa823401a11c0540b4893fde83eed2b247e3d2 | 12; typed visual settings; retired convenience preferences remain historical |
| Profiles | f6288d82a612eac4d16d0963384bee12104d2f33 | 3; user-named profiles/IDs are configuration, not diagnostic material; no current public status contract |
| Classy | 694870aa945b6ece95cf0b4343fc428eeb521853 | 5; exact gear-set identities must not be guessed or rewritten by Hub |
| Relay | 64544ad9bcc386d6f92a05a963c07625f99604d8 | 5; DPAPI webhook/bot secrets, snowflakes, character keys/names/worlds and keywords excluded; presentation-only backup |
| Core | e6527400680cddc39803321792b7c3b02fb0501d | Bundled per consumer, not a mandatory global runtime. Hub pins Core/UI 0.4.1 |

## Architecture

Separate services own health, public version projection, diagnostic package creation, compatibility rules, inspection providers, pause coordination, typed bounded activity and configuration backup. MainWindow only composes panels. Existing saved page IDs and utility preferences remain stable. No Mount/Fashion page redesign or metadata replacement is part of this train.

Health separates installation, loaded copy, development override, safe response and feature readiness. Public versions are fetched only on explicit request from the fixed central catalog URL, bounded and projected to known numeric versions. A missing optional endpoint is unavailable, never ready. Direct Faloop is dormant in current SRank source; a report-provider DLL does not prove a live feed.

Diagnostics use explicit projections, never raw config or arbitrary provider text. No names, content/account/entity IDs, paths, exception messages, chat, webhook URLs or credential values are eligible. Activity uses known event codes and severities with bounded retention/deduplication, not an arbitrary remote logging API.

Pause will use optional versioned acknowledgements, preserve saved enable preferences, and leave passive rendering/collection/Profiles/Relay reception usable. Unknown/older consumers cannot be represented as stopped. Consumer changes, if required, are held as coordinated proving candidates rather than silently deployed during this Hub train.

Backup is a separate local operation, never part of diagnostics. Each owner/schema has an explicit field allowlist. Restore must validate the owning identity/schema, refuse loaded external owners, preserve unknown/current excluded fields, and write one owner's file atomically after creating a sanitized pre-restore backup. No arbitrary paths, archive extraction, credential copying or plugin reload is permitted. Unreviewed schemas fail closed.

## Game/API and rights audit

Current [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) (MIT) exposes Character.EmoteController.EmoteId, separate pose state and explicit CharacterModes.EmoteLoop. Current [Dalamud IUnlockState](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IUnlockState.cs) exposes IsEmoteUnlocked and IsBuddyEquipUnlocked. API declarations/semantics only; no Dalamud AGPL implementation is copied. Local Lumina Emote/TextCommand/BuddyEquip rows provide names/icons/commands and item links where explicit. Animation/timeline/pose IDs are not substituted for emote IDs.

FFXIVClientStructs local UI Buddy.CompanionInfo exposes barding row IDs, but that is **own-character UI state**, not another player's equipment. Character.CompanionData is minion/follow-mount state. AgentInspect contains barding strings tied to an inspect request, not a verified current remote row-ID source. No remote mapping is accepted merely because model numbers resemble BuddyEquip IDs. Remote barding remains unavailable until exact current-build mapping/ownership/lifetime validation can be established. This limitation is preferable to guessed output.

[HaselTweaks README](https://github.com/Haselnussbomber/HaselTweaks) documents /whatemote and /whatbarding behavior (AGPL-3.0); only behavioral/reference evidence, no source/data incorporation. WhichMount remains behavioral precedent only. TextAdvance public IPC declarations were reviewed for read-only enabled/paused/feature flags; no external implementation is imported and Hub never toggles another plugin to resolve overlap.

**Metadata proposal before any new import:** use local sheets first; existing permission for FFXIV Collect does not automatically import its barding/emote database into this train. No new external acquisition dataset is proposed initially. Missing source/patch/obtainability stays Unknown; explicitly related local items/quests/shops may provide acquisition evidence without proving current access. Any later dataset requires a separate rights/provenance report before import.

## Consolidated supervised gate

One combined proving candidate must cover Health/dependency loss and recovery, sanitized diagnostic ZIP, overlap self-suspension, existing Mount/Fashion regression, inspector lifetime/none behavior, supported emote start/stop, supported barding or explicit unsupported state, pause acknowledgements/resume/passive functionality, bounded activity, backup and supervised restore preserving newer/unrelated settings. Automated fixtures do not establish live game correctness. Do not merge/publish this train until that required gate passes.
