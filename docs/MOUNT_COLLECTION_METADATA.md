# Mount Collection: data audit and provenance

Audit date: 2026-10-17 UTC. No third-party mount dataset was imported.

## Boundary

WhichMount (megurte/WhichMount, AGPL-3.0) was inspected through its public README at 3f66d8d0486fa9babfa889840f6d5d9d3e1ea556 for behavior only: searchable/sortable database and player context lookup, plus seats/actions/BGM/obtainability information. No WhichMount C# implementation, embedded MountList.csv, other resource/data file or generated derivative was read into or copied into Sentinel. Its target tooltip/hook behavior is outside this version.

The maintained source is **SentinelMountMetadata.cs**, independently authored in this task: six small factual collection definitions, exact English game-data keys, expansion assignment and official patch-note provenance. It is not an import or conversion of WhichMount, FFXIV Collect, XIVAPI, a wiki table or another mount dataset. UI descriptions/policies are original. No third-party icons/music/text dataset is bundled; game content is resolved locally through Lumina at runtime. Existing Dalamud/Lumina/FFXIVClientStructs dependencies retain their respective licenses; API names/signatures were consulted without copying their implementations.

Any future external dataset proposal must identify source, exact version, license, attribution/distribution requirements and intended fields **before import**. A public API or a public GitHub repository is not by itself permission to redistribute a dataset.

## Coverage audit

| Information | Source | Limit |
|---|---|---|
| Mount names/icons/journal candidates | Mount.Singular, Icon, ModelChara, Order | Includes named model/icon rows with nonnegative unlock order; actual Mount Guide denominator awaits live comparison |
| Seats | Mount.ExtraSeats + rider | No hardcoded seat table |
| Actions | Mount.MountAction → MountAction.Action → Action.Name | Read/display only |
| BGM | Mount.RideBGM; frequency across included mounts | Track ID and sharing count; no invented title or whole-game exclusivity |
| Own-character unlocks | Public IUnlockState.IsMountUnlocked, guarded by IPlayerState.IsLoaded, login/local-player/transition state | Unavailable is unknown; ephemeral snapshots clear on logout/change/zone/failure |
| Unlock items | Item.ItemAction → action 1322 and Data[0] mount key | Only verified matching local row; no name guessing |
| Market-board eligibility | Mapped Item.IsUntradable and ItemSearchCategory | Game eligibility, not stock/prices; no mapped item means unknown |
| Acquisition | Explicit Quest rewards (direct-item types only), OptionalItemReward, Achievement.Item and SpecialShop receive-item links | Names/row provenance from game data; no vendor-location inference, drop table, historical source or current-availability claim |
| Release expansion | Sentinel-authored six track member/reward definitions | Mount has no release-expansion field; no ID/item-level/quest-location heuristic. Unmapped mounts stay Unknown |
| Current obtainability | Reserved typed, separately verifiable metadata | **No authoritative current global availability dataset included; all entries currently Unknown** |
| Trial/meta tracks | Six original factual definitions below, exact English keys resolved against English Mount sheet | Missing/ambiguous keys remain unknown and remain in the denominator; prerequisites do not certify quest eligibility |
| Totem progress | Simple SpecialShop item exchange for known trial members; four loaded main bags only | Ordinary currency mode/direct item cost, one untradable Totem cost, one non-HQ reward; exact quantity only. Unsupported/partial state remains unknown; no retainers/saddlebag |
| Other player's ridden mount | Public IPlayerCharacter.CurrentMount from fresh default player context target | Click revalidates object/session/territory/option. No collection lookup or retained native pointer; no target hook |

Typed API/schema audit: [Dalamud](https://github.com/goatcorp/Dalamud/tree/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916), IUnlockState/IPlayerState/IContextMenu/ICharacter/ITextureProvider; [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/tree/d6397a76b3a63c2b247025dc296653182b718ca6), InventoryManager/InventoryContainer/InventoryItem; [EXDSchema](https://github.com/xivdev/EXDSchema/tree/83e965d091116f895d5b17573cc5d12909a5f407), Mount/MountAction/Item/ItemAction/Quest/Achievement/SpecialShop.

## Independently authored track facts

The following official patch notes identify the collection quest and required mount set. Only factual lookup keys and original Sentinel descriptions are committed; no patch-note prose/table, wiki dataset or third-party generated collection file is bundled. The reward is additionally cross-checked through local quest/item relationships where present.

| Expansion | Reward / collection quest | Official provenance |
|---|---|---|
| A Realm Reborn | Kirin / A Legend for a Legend | [2.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/5d677f92bbe71478ffaccd9972735b3d4280abe6) |
| Heavensward | Firebird / Fiery Wings, Fiery Hearts | [3.5 notes](https://jp.finalfantasyxiv.com/lodestone/topics/detail/17a38621ab0637f4ec5fbb329a8fb18b38ee09c3) |
| Stormblood | Kamuy of the Nine Tails / A Lone Wolf No More | [4.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/97afed7c0efbbe8260755f948d0cf2b912d71121) |
| Shadowbringers | Landerwaffe / The Dragon Made | [5.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/1cc2ab61c569bf2d07deeb95d7fb407013d3d9df/) |
| Endwalker | Apocryphal Bahamut / Wings of Hope | [6.5 notes](https://jp.finalfantasyxiv.com/lodestone/topics/detail/e17ce5b98068f6972379cef5adbc6c4b664f9780) |
| Dawntrail | Wings of Legacy / The Wing Spirit Cometh | [7.5 notes](https://fr.finalfantasyxiv.com/lodestone/topics/detail/9beb8a7b5c46944cd80ed92b2b8e972395fbea80) |

Tests enforce unique keys, six explicit expansion sets and provenance, plus unresolved/ambiguous lookup handling. This is a deliberately limited metadata layer over an automatically populated local mount database; expansion/source/obtainability coverage is not advertised as complete.
