# Coordinated Hub operations expansion — audit and boundaries

Candidate **0.7.0.0**, integration branch `codex/hub-operations-integration`, [Hub PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15). Live baseline remains **0.6.0.0** at `9aec4202373bee5e3b10adda35287987c6340b38`. This train must not merge/publish until the consolidated native gate passes. Phase artifacts are development candidates, not public releases.

## Live owner/config audit

| Owner | Audited source | Config schema / safe scope |
|---|---|---|
| Hub | 9aec4202373bee5e3b10adda35287987c6340b38 | 1; reviewed preferences, pause latch excluded from backup |
| SRank | e2ea4cbec8f8a1355bafc5690b58a3f4588e2255 | 16; reviewed switches/distances; auth/session/feed/history excluded |
| PvP | 0971ecdc53f5b4b5582295aab1748742164ae522 | 19; reviewed switches/navigation; exact mount identity and unreviewed native tuning excluded |
| HUD | f6aa823401a11c0540b4893fde83eed2b247e3d2 | 12; reviewed visuals/layout; retired convenience and unreviewed fields excluded |
| Profiles | f6288d82a612eac4d16d0963384bee12104d2f33 | 3; presentation only; profile definitions/names/IDs/selection excluded |
| Classy | 694870aa945b6ece95cf0b4343fc428eeb521853 | 5; presentation/position only; exact gear-set identities/names/maps excluded |
| Relay | 64544ad9bcc386d6f92a05a963c07625f99604d8 | 5; presentation only; entire CharacterProfiles subtree excluded |
| Core | e6527400680cddc39803321792b7c3b02fb0501d | Bundled per consumer; no global Core runtime/config assumed. Hub Core/UI pins remain 0.4.1 |

BackupRegistry.cs is the executable field/type/range allowlist. Backups reject other schemas/builds, including newer builds with the same schema until reviewed. The UI lists fields, exclusions and skipped owners. Native Dalamud window files, arbitrary directories and full consumer configurations are not copied. Relay ciphertext, webhook/bot credentials, Discord snowflakes, character identities, keywords and channel settings never enter a backup. Current excluded/unrelated fields are preserved during restore.

## Services and ownership

Health, public version projection, diagnostics composition, compatibility, live inspection, pause coordination, activity and backup each have separate policies/services/panels. MainWindow composes them. Saved page IDs/window identity and Core's canonical UI remain intact. Extra pages use Core's existing rail-size option; no Core renderer/theme copy. Mount/Fashion page source, metadata, tracks and token logic are unchanged.

Health distinguishes installed/live inventory from the actually loaded development copy, public catalog version, internal enable/readiness and IPC response. Public versions require an explicit fixed-URL bounded fetch; failed reads become Unknown. A loaded DLL is never sufficient for Ready. vnavmesh readiness, Lifestream/RSR/Splatoon responses and unknown HuntAlerts/Sonar feed state are distinct. Direct Faloop is dormant in audited SRank 0.7.62.0, not inferred from report-provider installation. The existing character-scoped SRank Remote.v1.Status endpoint is queried read-only while your character is loaded; only loaded/enabled/ready, bounded queue count and recovering primitives survive projection. Names, character keys, hunts and status strings are discarded. Other older consumers without status endpoints stay explicitly unavailable/unknown.

Diagnostics creates `sentinel-diagnostics-UTC-timestamp.zip` containing report.json, hub-state.json and summary.txt. It reconstructs allowlisted IDs, numeric versions, enums, bounded counters and public territory/world rows. It includes typed health, compatibility, pause and recent event observations, not arbitrary provider strings. No config, chat, credentials/ciphertext, paths, exception messages, names, content/account/entity IDs or inspection identities are exported. Known-secret canaries cover provider, health, event, pause and backup boundaries. Diagnostics is separate from settings backups.

Compatibility reads reviewed TextAdvance enabled/paused/feature IPC. Definite overlap suspends only the affected Hub feature; unavailable known-provider state is a clearly labeled conservative suspension. Loaded NoClippy is a possible overlap because module activity is not exposed; existing native XivAlexander markers block compensation. YesAlready and multipurpose tweak plugins remain possible/informational where individual feature state is unverified. Third-party plugins/settings are never changed. Hub effective options are derived without overwriting saved preferences, and recover when overlap clears.

## Current game/API verification

Current FFXIVClientStructs (MIT) supplies typed Character, EmoteController, MountContainer, DrawDataContainer, GameObject and enum declarations. Dalamud API 15 supplies object validity/identity/context menus, framework-thread checks and IUnlockState.IsEmoteUnlocked/IsBuddyEquipUnlocked. Product/native fixture binding is FFXIVClientStructs **7.57.0.9386**. API declarations/field semantics only; no AGPL implementation is incorporated.

One `Sentinel Hub: Inspect player` subscription preserves Mount/Fashion opt-ins and adds a default-OFF broader inspector. Menu callbacks retain numeric tickets, not actor references/pointers/names. Framework processing re-fetches object/entity/slot and own session/territory, rejects old clicks and loading/transformed/stale actors, and clears prior results before a new request. The Inspector observes while visible, clearing on close/off/logout/zone/despawn/identity reuse or five-minute expiry. A menu opening a closed/minimized Hub evaluates visibility after opening. Mount/Fashion-only mode keeps its existing collection-page workflow.

**Emotes:** current explicit EmoteController.EmoteId must have a usable local Emote/TextCommand entry, CharacterModes.EmoteLoop and matching Emote.EmoteMode / actor ModeParam. Ordinary idle, pose/InPositionLoop, movement, combat and action-timeline IDs are never substituted. Short one-shot emotes may be missed and deliberately remain unidentified. The view refreshes to None when a supported loop ends. No emote playback/native hook is added.

**Barding:** own UI Buddy.CompanionInfo barding IDs are not another player's equipment; AgentInspect strings are not treated as live row IDs. The independent reader finds exactly one loaded BattleNpc/Buddy owned by the inspected player's entity, or a validated object-table Mount linked to their Company Chocobo (mount row 1). It never follows an unvalidated mount pointer. Typed head/body/feet EquipmentModelId.Value fields match the full local BuddyEquip.ModelTop/ModelBody/ModelLegs values. It does not strip unknown bits, substitute model values for row IDs or select an ambiguous alias. Identity/draw/kind/owner/transformation and current mount relationship are rechecked. Missing, ambiguous or changed state fails closed; other mounts are not assumed to be customizable chocobos. Head/body/leg icons/names and own unlock state come from local BuddyEquip. Native mapping/replication still requires the in-game gate.

HaselTweaks AGPL README describes /whatemote and /whatbarding; referenced public field/slot symbols were consulted as interoperability facts, not imported implementation/data. Matching/lifetime/ambiguity policies and tests are Sentinel-owned. WhichMount remains behavioral precedent only. No manual offsets, signatures, new unmanaged hook, target-tooltip patch, equip/execute action or external character query was added. Existing accepted latency hooks remain isolated.

## Acquisition/source rights and provenance

The initial game-only proposal was expanded and reported in chat **before** fetching/importing the new metadata. FFXIV Collect's creator explicitly invites API data use in other projects: [service/developer invitation](https://ffxivcollect.com/), [creator statement](https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/), [API docs](https://ffxivcollect.com/api/docs). This permission is separate from the application's MIT software license, not a blanket database license inferred from public access. No wiki/unlicensed/copyleft dataset, artwork, description, user statistic/profile or market-price data is imported. Attribution is in UI source text, embedded headers and THIRD_PARTY_NOTICES.md.

| Snapshot | Retrieved UTC | Records / exact public game identity matches | Original response SHA256 |
|---|---|---|---|
| https://ffxivcollect.com/api/bardings?language=en | 2026-10-18T16:58:18.618804+00:00 | 107 / 107 | b6d80bc9b952c4af922543710f1335bb1dad0a3fe428d6d667d5a79a1052ecc9 |
| https://ffxivcollect.com/api/emotes?language=en | 2026-10-18T16:58:19.147093+00:00 | 201 / 201 | b7d257687963a98d8d910329b2a06ed790d4adadd4d794578943f3c4103dd153 |

The review-only workflow cross-checked full identity sets against public BuddyEquip/Emote game views (game version 5c744a8d8daafb25); raw game tables are not bundled. Game cross-check response hashes: BuddyEquip `ed4cfb2fd0ba8a82c893171b4ebfd9ed75f4158c670d5d3200df95e99d1e40cc`, Emote `4d25d916eec3ea5430e25b95a00ecf803075290d8874488d5dd6cb7b0e46921f`. [Audit run](https://github.com/MarshalTitan/SentinelHub/actions/runs/39147407586).

Only ID, English identity, source type/text and introduction patch are kept. Runtime requires exact numeric ID and case-insensitive English identity against the installed client; drift/missing metadata stays Unknown. The 201 records are unlockable emotes, not every built-in/variant game emote. Local action 2633 links unlock-link items to Emote.UnlockLink; 1013 links BuddyEquip unlock items. Explicit quest/achievement/gil-shop links add source evidence. Shop presence is not proof of current access. Source/category and patch are independently maintained JSON resources; replacement requires provenance review and a normal tested release, never automatic runtime import. Event/promotion text may be historical. Recorded Online Store is a source classification, not live regional stock. **Current obtainability stays Unknown** in this module; no release/availability inference from sheet presence or source labels.

## Cooperative pause and optional activity contract

Current public SRank/PvP builds do not implement the new pause contract. Hub visibly reports **Unsupported / may still automate**; their own stop controls remain required. Do not claim a global stop from a sent request. Hub suspends its own Anti-AFK, Quest pending work and latency writers while retaining preferences. Passive rendering/settings/collections/inspection, Profiles management and Relay receiving remain usable. Pause is persistent; resume releases the latch and applies existing preferences/compatibility/readiness gates rather than enabling anything.

Optional consumer protocol: `Sentinel.<InternalName>.PauseProtocol.v1` returns int 1; `.PauseSet.v1` accepts `(string callerLease, bool paused)` and returns acceptance; `.PauseIsPaused.v1` returns actual quiescence. A consumer must acknowledge only after its owned work stops, isolate caller leases, provide an owner-local release path and remain independent of Hub. Hub never resumes a lease it did not acquire. Acceptance without observed pause stays Pending; missing/rejected/failed participants remain explicit. `SentinelHub.AutomationPaused.v1` exposes desired pause for optional startup guards, not remote acknowledgement. No consumer runtime/package update is silently bundled in this train.

`SentinelHub.Activity.Publish.v1(string source, int code) -> bool` accepts only loaded known Sentinel sources and allowed typed codes on the framework thread. It accepts no arbitrary message payload. Source reports are advisory, not authenticated proof of gameplay or pause. Optional health int and diagnostic JSON v1 contracts are documented by their strict parser/projection. Current Profiles/Relay/SRank gameplay events are displayed only when a compatible source publishes them; no private reflection/config polling invents those events.

Activity retains at most 200 typed events for seven days, groups repeated source/code events within a minute, caps counts, and saves sanitized state periodically/at unload. Invalid/unknown records are discarded. Normal health/compatibility transitions, own pause requests/acknowledgements, export and backup results publish meaningful fixed messages. It is not a permanent raw log database.

## Restore transaction and proving

All backup data fields are reviewed non-text scalars with bounded values; owner/build/schema, duplicate keys/owners, checksums, malformed data and linked paths are checked. External owners must stay unloaded; development overrides and unreviewed builds reject restore. Each operation restores one owner, preserves current excluded/unrelated fields, writes a sanitized pre-restore backup first, rechecks version/load/current bytes, then atomically replaces the file. Replacement failure preserves original bytes; temporary files are removed. Hub preparation occurs before replacement, its existing settings object is updated synchronously, and it remains paused. No auto reload/restart, multi-owner partial restore, raw rollback config copy or arbitrary archive extraction occurs.

Automated policies, real native ImGui cases, owned-memory reader fixtures and installable artifact validation are required. They do not prove live game replication, supported emote start/stop, real companion model mapping, native pause correctness or actual owner config restoration. See [the consolidated proving procedure](OPERATIONS_ACCEPTANCE.md). Rollback: turn Hub utility preferences off while paused, unload the isolated candidate and re-enable immutable live Hub 0.6.0.0; keep user configuration and backups.
