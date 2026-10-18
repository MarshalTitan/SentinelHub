# Fashion Accessories: source, API and rights audit

Audit: 2026-10-18 UTC, before implementation/import. Hub baseline 0.5.1.0 at 9dea4ad1d8477eedd35196e38eff7e5a37f2344d; delivery 0.6.0.0.

## Findings and boundaries

The source proposal was reported in chat before the snapshot was fetched. No WhichMount implementation, CSV or derived dataset was imported. Its README's player-identification concept is behavioral precedent only. No ornament, target-info or tooltip hook is introduced. All collection and identification code is Sentinel-owned.

| Information | Reliable input | Limit |
|---|---|---|
| Catalog IDs, localized/English names, icons, model/order | Installed Lumina Ornament sheet | Real catalog rows require nonzero ID/name/icon/model and nonnegative order. Not proof of release or obtainability |
| Other player's active ornament | Current typed Character.OrnamentData.OrnamentId | No public current-ornament property was found in current Dalamud ICharacter/IPlayerCharacter. Framework-thread read-only adapter, not copied offsets |
| Auto umbrella | Separate AutoUmbrellaOrnamentId preference | Deliberately never used as the active accessory |
| Own unlock | Public IUnlockState.IsOrnamentUnlocked(Ornament) | Only while logged in/player loaded/not zoning; unavailable is Unknown, never false/Locked |
| Unlock item | Item.ItemAction.Action == 20086 and Data[0] ornament key | Only links to known current catalog IDs; no name guessing or API item-ID override |
| Trade / market | Local IsUntradable and ItemSearchCategory on mapped unlock items | Eligibility only, no live listings/prices. Missing mappings stay Unknown |
| Quest/achievement/vendor/exchange | Explicit local rewards, GilShopItem/GilShop, SpecialShop receive/cost items | NQ base price and explicit conditions; retained shop/reward rows do not prove current access |
| Patch / expansion | Dated permitted FFXIV Collect snapshot, exact ID + English identity | Community-maintained patch, major 5/6/7 maps to Shadowbringers/Endwalker/Dawntrail. Unknown future/malformed stays Unknown |
| Acquisition/category | Attributed API text plus independent official corrections | Not fabricated; event/campaign text retained. No wiki tables imported |
| Cash shop | Explicit Online Store source, separately labelled as recorded | Two API records currently carry it. Absence stays Unknown, not No; no claim of current stock |
| Current obtainability | Dated independently authored official evidence with expiry | One gil-sale route recorded Yes as of audit; all other entries Unknown. No globally verified No records. Not character eligibility |
| Retired spectacles / aliases | Official 7.0 conversion notes plus verified row IDs | Excluded from current collection; no mapping of Facewear into ornaments |

## Current API / structure verification

- [Dalamud f7d06f363955251991222363a7c008b8998b6565](https://github.com/goatcorp/Dalamud/tree/f7d06f363955251991222363a7c008b8998b6565): public IUnlockState, IFramework.IsInFrameworkUpdateThread, IGameObject validity/identity and IContextMenu. API definitions/semantics were consulted; no AGPL implementation is incorporated.
- [FFXIVClientStructs d6397a76b3a63c2b247025dc296653182b718ca6](https://github.com/aers/FFXIVClientStructs/tree/d6397a76b3a63c2b247025dc296653182b718ca6) (MIT): Character, OrnamentContainer, CharacterData, TransformationContainer and ModelContainer. Current OrnamentData has active OrnamentId and separate auto-umbrella preference. Sentinel uses the SDK-delivered typed fields, not manual offsets, signatures or SetupOrnament calls.
- [EXDSchema](https://github.com/xivdev/EXDSchema/tree/83e965d091116f895d5b17573cc5d12909a5f407): Ornament/Item/ItemAction/reward/shop field semantics.
- Product CI compiles against current delivered Dalamud API 15 fixtures and exercises the actual typed reader using owned fixture memory. This verifies binding/guard behavior, **not live game replication or all ornament types**. Supervised parasol/wings/none checks remain required.

The adapter is invoked only for an opted-in lookup on the framework thread. The menu captures numeric object/entity/slot/session/territory identifiers and a timestamp, not names or pointers. A click is queued; the framework re-fetches the player, rejects a ticket older than 10 seconds and validates identity/session/territory/options/lifetime. It checks targetability, draw-object presence, loading/effect-pending state, transformation ID/name/model/flags, reads only the active scalar, then rechecks the object-table entry. No ornament-object pointer is dereferenced, and no actor reference survives the synchronous read. These guards reduce stale-state risk; they do not constitute a promise about arbitrary modified clients. Suppressed/hidden rendering is not reconstructed: the result is the actor's active ornament state, not a screenshot classifier.

## Complete current identity cross-check

A review-only cloud audit fetched the full public Ornament sheet through XIVAPI v2 and compared it with the permitted acquisition snapshot:

- Game data endpoint: https://v2.xivapi.com/api/sheet/Ornament?fields=Singular,Icon,Model,Order&limit=500
- Response hash: 3343c402b863e38c2a575a80bc67451641ecd6ab3bc94ac647acf15c4fe48601
- Game data version: 5c744a8d8daafb25; service schema: exdschema@2:rev:e773c41a90aed788cf4c1c48469fa85618ef01fb.
- 59 rows (0–58): three blank/invalid rows, four retired spectacles, **52 current catalog identities**. All 52 API identity names/IDs match the corresponding public game rows.
- Retired IDs 22/25/26/32 remain populated and would otherwise incorrectly count as missing. [Official 7.0 notes](https://na.finalfantasyxiv.com/lodestone/topics/detail/d7db61f938f9cea65e4c5cd261918edb036b3004) identify the conversion of False Oval/Shaded/Classic and Rose-colored Spectacles to Facewear. Row 22 retains the English label False Spectacles; this alias is documented, not fuzzy-matched.
- Current ID roster: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 23, 24, 27, 28, 30, 31, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58.
- Mandragora Parasol (#42) has no verified acquisition text and stays Unknown. This is not a claim that it is released, obtainable or discontinued.
- Great Paraserpent / Spriggan Parasol are recorded contest/event prizes; a prize source is not a currently open contest. Bomb Parasol retains historical PvP Series 6 text without an invented current source.
- Currency text is retained as provided: Avalanche Gatling Gun's source says 430 MGC, not an inferred MGP value. Currency aliases below item ID 8 remain unresolved in local special shops.
- Names such as Statice's Wings, Bluepowder Pixie Wings and the separate wing mounts are matched by exact IDs/names; wings are not assumed to be mounts.
- No raw third-party game-row dataset, images or game flavor descriptions are bundled. The numeric roster and official conversion facts are independently maintained audit facts; runtime rows are local Lumina. XIVAPI served only as a read-only cross-check.

## Metadata rights and provenance

The imported source is **FFXIV Collect's public API under the creator's explicit data reuse invitation**, not an assumption that public access or the application's MIT license permits any dataset:

- [Homepage developer/API invitation](https://ffxivcollect.com/).
- [Creator's explicit statement allowing API data use in other projects](https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/).
- [API documentation](https://ffxivcollect.com/api/docs).
- [Application README/license f4a9c97bff0673e7a48b6f6370b00926979c9e1b](https://github.com/fae-industries/ffxiv-collect/tree/f4a9c97bff0673e7a48b6f6370b00926979c9e1b) reviewed separately. Its MIT software license is not represented as a blanket live-database license. Its AGENTS.md says the upstream project does not accept AI-generated contributions; that was reported to the user. No upstream changes are made.

Snapshot endpoint: https://ffxivcollect.com/api/fashions?language=en  
Retrieved: 2026-10-18T15:02:59.902094+00:00  
Original response SHA256: 3b08be4c5f89c660053570fd36470548327a8d6c58be1c7e05bf73402140d707

Kept fields: ornament ID, English identity, patch, reported item ID (audit only), short source type/text. **52 records, 51 with acquisition text, two with an explicit Online Store source**. No ownership percentages, user profiles, descriptions, assets or market prices are imported. Runtime item mappings/eligibility come from local sheets, not the API's tradeable/item fields. Attribution is visible in sources and THIRD_PARTY_NOTICES.md.

Corrections are original Sentinel factual records in Fashion/Data/Corrections.json, separately maintainable from implementation. [Vermilion Paper Parasol's official item page](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/41cb3c6b931/) lists a 100,000-gil sale route; the dated global route evidence expires 2026-11-17, after which the filter/display becomes Unknown. No character access claim. [Red Moon Parasol's official store page](https://store.finalfantasyxiv.com/ffxivstore/en-gb/product/882) supplies a direct source link, but the historical event/store pair does not infer global live availability. The second recorded store accessory (Archon Egg Parasol) uses its attributed reference page until an individual official product URL is verified; no product URL is guessed.

Other sources compared for coverage only: [Gamer Escape category](https://ffxiv.gamerescape.com/wiki/Category:Fashion_Accessory), [community wiki](https://ffxiv.consolegameswiki.com/wiki/Fashion_Accessories), and official patch/award pages. No wiki/community-guide dataset is imported where redistribution rights are unclear.

## Public project references

- AutoVisor, Apache-2.0: consulted for evidence that current ornament state is read from the character container. No visibility automation/implementation is copied.
- HaselTweaks/HaselDebug, AGPL-3.0: README/behavior and repository metadata only; no implementation/data imported.
- Brio, GPL-3.0: README behavior (attaching ornament actors) only; not a source for Sentinel's live-player adapter.
- CollectorsAnxiety, AGPL-3.0: repository/license and search/API-reference evidence only; no implementation/data copied.
- WhichMount, AGPL-3.0: prior behavioral precedent only. Target-info/tooltip modifications remain out of scope.

## Maintenance and acceptance

Review workflow Audit fashion accessory sources produces a candidate source artifact and a numeric identity cross-check; it never commits/deploys automatically. The plugin has no acquisition network dependency. Review source rights, ID/name mismatches, new/retired rows, patches/categories and dated corrections before replacing resources in a normal tested release. Availability values require a primary-source URL, checked/expiry dates and evidence; absence/expiry/malformed evidence is Unknown.

See FASHION_ACCESSORIES.md for supervised acceptance. No automated fixture or catalog publication establishes a live-player recognition PASS.
