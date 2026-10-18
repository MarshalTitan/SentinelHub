# Mount acquisition data acknowledgments

Mount acquisition source facts and introduction patches in Mounts/Data/Acquisition.json are a dated, reduced snapshot from **FFXIV Collect**, created by Sera Fae / Raelys (Matt Antonelli), with community contributors.

- Service and developer API invitation: https://ffxivcollect.com/
- Explicit creator statement allowing API data use in other projects: https://www.reddit.com/r/ffxiv/comments/i75cb0/ffxiv_collect_whats_new/
- API documentation: https://ffxivcollect.com/api/docs
- Snapshot and original-response digest: see docs/MOUNT_COLLECTION_METADATA.md and the embedded JSON header.

This use relies on the creator's data reuse statement. The FFXIV Collect application's MIT software license is separate; no application source is incorporated. No claim is made that its software license grants a blanket license to unrelated third-party data, artwork or game assets.

Store product URLs/titles and Edelina's vendor facts were independently recorded from official Square Enix sources. No store descriptions, product images, game flavor descriptions, market prices, ownership statistics or character data are imported.

FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd. FINAL FANTASY XIV © SQUARE ENIX CO., LTD. Game names/icons/actions are obtained from the user's installed game. Existing Dalamud/Lumina/FFXIVClientStructs dependency licenses remain applicable.

WhichMount's AGPL implementation and embedded dataset are not used.

## Fashion Accessories metadata

Fashion/Data/Acquisition.json is a reduced, attributed snapshot of FFXIV Collect's fashions API, retrieved 2026-10-18 under the same explicit API data reuse invitation above. Creator/community credits remain applicable. It contains ID/English identity/patch/reported item/source facts only; no images, flavor descriptions, user collections or market data. This is separate from the application's MIT license. See docs/FASHION_ACCESSORIES_AUDIT.md for the exact hash, fields, source comparison and limitations.

Fashion/Data/Corrections.json contains independently authored factual records from linked official Square Enix pages. The public game sheet was consulted for numeric row coverage; no raw game-row dataset is bundled. Current typed FFXIVClientStructs fields (MIT dependency) and public Dalamud APIs are used without copied implementation. AutoVisor (Apache-2.0) was consulted for field semantics only; non-permissive reference projects supply behavior only.


## Player Inspector acquisition facts

The 2026-10-18 FFXIV Collect API review snapshot in Inspection/Data contains short acquisition/introduction-patch facts for 201 unlockable emotes, under the creator's explicit API data reuse invitation linked above. This is not an assumption that the application's MIT license licenses its entire live database. Names/icons/runtime IDs remain local Lumina. No images, descriptions, user statistics, profiles, market prices or current-obtainability flags are imported. Exact numeric ID plus English game identity gates all application of source metadata. Snapshot timestamps/original-response SHA256 and API endpoints are embedded; the review workflow cross-checks identities against a public game-sheet view without bundling that raw table. No WhichMount/HaselTweaks implementation or AGPL dataset is incorporated. Public API/field names consulted for interoperability are distinguished from incorporated implementation. See docs/OPERATIONS_EXPANSION.md for audit, limits and source links.
