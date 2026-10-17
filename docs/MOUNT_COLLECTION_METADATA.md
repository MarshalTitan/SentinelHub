# Mount Collection: data audit and provenance

Audit date: 2026-10-17 UTC. Hub 0.5.1.0 adds an attributed, frozen acquisition snapshot to the original game-data browser.

## Independent implementation boundary

WhichMount (megurte/WhichMount, AGPL-3.0) remains a behavioral README reference only, at 3f66d8d0486fa9babfa889840f6d5d9d3e1ea556. Its implementation, MountList.csv, resources and generated derivatives are excluded. No target-info hook or tooltip modification is implemented.

Sentinel's loader, identity matching, filters, shop traversal, source formatting, tests and refresh script are independently implemented. The small six-track definitions remain Sentinel-authored from official patch-note facts. Names/icons/actions/music and character state are still read locally through Lumina/Dalamud; no external game images, music, flavor descriptions or character data are bundled.

## Acquisition source selected before import

The source proposal was reported to the user before the API snapshot was fetched. This import relies on the creator's **explicit API data reuse invitation**, not merely the application's MIT license or the API being publicly reachable:

- [FFXIV Collect homepage](https://ffxivcollect.com/), API section, makes all site data available to developers through its public API.
- [Creator's published reuse statement](https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/), Other stuff, explicitly makes the data available for use in other projects via the API.
- [API documentation](https://ffxivcollect.com/api/docs).
- [Application README and MIT license](https://github.com/fae-industries/ffxiv-collect/tree/f4a9c97bff0673e7a48b6f6370b00926979c9e1b) audited separately. That MIT software license is **not represented as a separate blanket license for the live community database**. No application source is incorporated.

The live API grant does not establish provenance or licensing for an unrelated mirror, wiki table or repackaged dataset. No such dataset was imported. Credits to FFXIV Collect, its creator and community contributors are retained in [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md), in each community source, and in the browser snapshot label.

### Frozen snapshot

- Endpoint: https://ffxivcollect.com/api/mounts?language=en
- Retrieved: 2026-10-17T05:29:56.813025+00:00
- Original response SHA256: 1f4d42317d9666fc30a2be2af56a1cd140c67f6937c2c16b36b56b026250f72f
- Kept fields: numeric mount ID, English identity name, introduction patch, acquisition type and short source text only.
- 356 API mount records, all with at least one acquisition source; 46 contain the explicit Online Store source.
- Source data is community-maintained, can be incorrect/outdated and is displayed as such. No current-availability flag is inferred.
- `Mounts/Data/Acquisition.json` is embedded into the assembly, not downloaded during play. Matching requires **both numeric ID and exact case-insensitive English mount identity** against the local client. Missing/different identities are rejected and counted; no fuzzy matching.
- Every applied introduction patch is attributed to this snapshot. Major patches 2 through 7 map to expansions 0 through 5; valid a/b patch suffixes are supported. Future/malformed values remain Unknown.
- Runtime shows source coverage over the actual local database, matched snapshot identities and rejected identities. 356 is the snapshot count, **not a claim that every local Mount row has complete verified acquisition information**.

### Independently maintained official facts

`Mounts/Data/StoreLinks.json` holds 45 reviewed mount ID/name to official store product-page mappings. It stores factual product titles/URLs only, no prices, marketing descriptions or assets. Reviewed title differences include Shagcoat Cat / Shagcat, Rising Phoenix / Reborn Phoenix, and Set of Ceruleum Balloons / Ceruleum Balloons. Bennu has a recorded Online Store source but uses the official mount category until its individual product URL is verified. Regional product URLs/prices/availability can change; the actual store remains authoritative.

Source pages are attached to those records. [Official mount catalog](https://store.finalfantasyxiv.com/ffxivstore/en-us/category/11), [Shagcat listing with Shagcoat Cat Whistle](https://store.finalfantasyxiv.com/ffxivstore/en-us/product/1100), [Reborn Phoenix](https://store.finalfantasyxiv.com/ffxivstore/en-us/product/1046), [Ceruleum Balloons](https://store.finalfantasyxiv.com/ffxivstore/en-us/product/910) and [Ambulario](https://store.finalfantasyxiv.com/ffxivstore/en-us/product/1190) were checked. Other product records carry their own primary-source URL.

Sentinel's original factual vendor record covers Edelina's Chrysomallos, Gilded Mikoshi and Magitek Avenger G1 unlock items: Mor Dhona (X:22.1, Y:4.8), 50,000,000 gil. Cross-checks: [official Edelina shop](https://eu.finalfantasyxiv.com/lodestone/playguide/db/shop/7fa3c777312/) and [official Chrysomallos Horn](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/735c5193706/). No tooltip/marketing prose is copied.

## Game-state versus maintained metadata

| Information | Source | Limit |
|---|---|---|
| Names/icons/journal candidates | Mount.Singular, Icon, ModelChara, Order | Actual Mount Guide denominator still needs supervised comparison |
| Seats/actions/BGM | ExtraSeats + rider; MountAction → Action; RideBGM | BGM ID/sharing only, no invented title or whole-game exclusivity |
| Own unlocks | Public IUnlockState.IsMountUnlocked with login/player/transition guards | Unknown on unread state; ephemeral snapshots clear on logout/change/zone/failure |
| Unlock items / market eligibility | ItemAction 1322 + Data[0]; IsUntradable + ItemSearchCategory | Market eligibility only, never live listings |
| Gil vendor entries | GilShopItem parent row → GilShop; mapped unlock item; Item.PriceMid | NQ base price only. HQ unresolved; quest/achievement/state/festival requirements shown when explicit. No universal vendor availability claim or guessed location |
| Exchange entries | SpecialShop receive items and costs | Direct item costs only in recognized modes 0/4/8/16 with ordinary cost type, no collectability, ID >=8; unsupported currencies/conditions explicitly unresolved |
| Quest/achievement rewards | Explicit Quest rewards / OptionalItemReward / Achievement.Item | A retained historical reward row does not certify current access |
| Acquisition methods | Attributed FFXIV Collect source facts + original official facts | In-game, Online Store, other premium/promotion and event sources remain distinct; source text retains conditions, currencies and event dates |
| Release expansion | Matched API introduction patch; original six-track fallback | Unmatched/invalid remains Unknown; no row-ID/item-level/location heuristic |
| Current availability | Separate tri-state | Still Unknown / Not checked globally. No active event, purchasability, store stock or account eligibility inference |
| Trial/meta tracks | Six original factual sets below | Exact English keys, unresolved/ambiguous remain in unknown denominator |
| Totem progress | Strict simple SpecialShop offers for known track members; four loaded main bags | Exact required cost, no estimated runs, retainers/saddlebag or automatic exchange |
| Other player's mount | Public current mount from live context player | Click revalidates session/object/territory/option; no other-player collection query or target hook |

Technical references: [Dalamud](https://github.com/goatcorp/Dalamud/tree/f3eeb5e18bd982d1ae928cbf6dd0653997d3a916), [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs/tree/d6397a76b3a63c2b247025dc296653182b718ca6), [EXDSchema](https://github.com/xivdev/EXDSchema/tree/83e965d091116f895d5b17573cc5d12909a5f407), [Lumina SubrowExcelSheet](https://github.com/NotAdam/Lumina/blob/master/src/Lumina/Excel/SubrowExcelSheet.cs).

[SaintCoinach GilShopItem / Item](https://github.com/xivapi/SaintCoinach/tree/0b22b78f97951bb66bfa78fb561aa4c7bf7af7cd/SaintCoinach/Xiv) (WTFPL repository) was consulted for field semantics: the parent gil-shop key and item ask price (PriceMid). Its [SpecialShopListing](https://github.com/xivapi/SaintCoinach/blob/0b22b78f97951bb66bfa78fb561aa4c7bf7af7cd/SaintCoinach/Xiv/SpecialShopListing.cs) documents compact aliases below 8 for modes 4/8/16. No converter/shop implementation or dataset was copied. Sentinel independently excludes unresolved aliases from named costs and token counting.

## Original expansion collection tracks

| Expansion | Reward / collection quest | Official provenance |
|---|---|---|
| A Realm Reborn | Kirin / A Legend for a Legend | [2.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/5d677f92bbe71478ffaccd9972735b3d4280abe6) |
| Heavensward | Firebird / Fiery Wings, Fiery Hearts | [3.5 notes](https://jp.finalfantasyxiv.com/lodestone/topics/detail/17a38621ab0637f4ec5fbb329a8fb18b38ee09c3) |
| Stormblood | Kamuy of the Nine Tails / A Lone Wolf No More | [4.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/97afed7c0efbbe8260755f948d0cf2b912d71121) |
| Shadowbringers | Landerwaffe / The Dragon Made | [5.5 notes](https://eu.finalfantasyxiv.com/lodestone/topics/detail/1cc2ab61c569bf2d07deeb95d7fb407013d3d9df/) |
| Endwalker | Apocryphal Bahamut / Wings of Hope | [6.5 notes](https://jp.finalfantasyxiv.com/lodestone/topics/detail/e17ce5b98068f6972379cef5adbc6c4b664f9780) |
| Dawntrail | Wings of Legacy / The Wing Spirit Cometh | [7.5 notes](https://fr.finalfantasyxiv.com/lodestone/topics/detail/9beb8a7b5c46944cd80ed92b2b8e972395fbea80) |


## Maintenance and review

Run the **Audit mount source snapshot** workflow manually, or `python .github/scripts/Import-MountSources.py --output mount-sources.json`. It fetches once, bounds/validates the complete response, strips unused content and creates a review artifact. It never modifies the repository, deployed plugin or character data automatically. Review source/permission changes, count/key/category/patch changes and spot-check official pages before replacing the embedded snapshot in a normal tested release. Refresh official links independently. Product builds and policy/UI tests use committed embedded resources and require no live acquisition service.

Tests cover every embedded record, strict identity, patch mapping, source classifications and URLs, official aliases, Chrysomallos gil regression, unknown cost rules and search/filter integration. Remaining supervised gates include live local-sheet completeness and prices, unlock counts, inventory and context behavior. New acquisition data does not establish a game PASS.
