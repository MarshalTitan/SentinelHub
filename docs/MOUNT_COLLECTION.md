# Mount Collection — Hub 0.5.0.0

Open /shub → Mount Collection. The database loads incrementally from the installed game's sheets on first use. It is independent of Anti-AFK, Quest Convenience, Latency/Weaving, Profiles and HUD. Existing options are unchanged.

Browse every local named/model/icon/nonnegative-unlock-order collection candidate. Search localized/English mount names, IDs, unlock items or known source text; sort by name, ID or seats. Overall completion uses this database's denominator, which requires comparison with the live Mount Guide. Expansion completion covers **verified mapped mounts only**, with the mapped fraction and Unknown expansion group visible. It must not be presented as a complete expansion catalog.

Names/icons/seats/actions/BGM relationships and unlock-item/market eligibility come from local game data. A BGM override can be unique among these mounts without being exclusive throughout the game. No music playback or guessed song titles. Marketable means a known unlock item has a market search category and is tradable, not a current market listing.

Acquisition includes explicit local quest, achievement and special-shop reward links, plus independently authored Sentinel collection metadata. An exchange entry is not proof that its vendor is currently accessible. **Current obtainability stays Unknown** until separately verified metadata is authored; verified-yes/no filters therefore may be empty. A missing source is Unknown, never a fabricated location or method. See [data audit and provenance](MOUNT_COLLECTION_METADATA.md).

Six expansion trial collection tracks (ARR through Dawntrail) use Sentinel-authored exact English game lookup keys. Track prerequisites and reward ownership are separate; owning all prerequisites does not claim story/quest eligibility or automatically grant a reward. Unresolved keys remain visible and count as unknown.

Trial token progress is available only for verified track members with a simple direct-item game-sheet exchange: one non-HQ reward, one ordinary untradable Totem cost, no alternate currency mode/complex costs. Exact required quantity comes from that offer, never an assumed 99. Counts read only four loaded main bags; unavailable/partial data is unknown. No retainers, saddlebag, other characters, estimated runs, item use, exchange or inventory movement.

Player context lookup defaults OFF. Enable **Player context-menu mount lookup**, right-click a nearby mounted player, then **View mount in Sentinel Hub**. It reads public current-mount state and opens that mount's details. Remote/offline friend entries, NPCs, inventory menus and unmounted characters do not offer it. The click revalidates the local character/session, territory and live target; stale/disposed/disabled callbacks cannot open. It does not inspect the other player's collection. No target-info hook or tooltip modification is installed.

## Short supervised acceptance

Update normally to **Hub 0.5.0.0**, keep existing utility settings, and:

1. Open Mount Collection. Let loading finish; search a known owned and a known missing mount. Check name/icon, unlocked/locked state and overall count against Mount Guide. Try sorting and Missing/Unlocked. Log out/in or switch character once if convenient: stale ownership must not remain visible as current.
2. At minimum window width in your theme, open details and expand a collection track. All controls/text must remain reachable by scrolling. Check a multi-seat/action/BGM example you know; expansion Unknown and limited mapping are expected for uncovered mounts.
3. If you carry a displayed trial totem, compare its count with the four main bags. Required cost must match the game vendor entry. Move a stack within main bags and refresh; it must count once. Otherwise mark token counting untested.
4. Enable player context lookup; inspect another nearby mounted player and select the Hub action. Correct mount details should open. Check unmounted player and option OFF; reload Hub once and repeat the mounted check. No duplicate menu entries, crash or effect on the other utilities.

Return PASS/FAIL, Hub version, theme, incorrect mount/item ID if any, and untested steps. Compilation/render tests do not establish actual game-sheet completeness, native inventory correctness or live context-menu behavior.

Rollback: disable the context lookup option, or disable Hub / restore immutable Hub 0.4.0.1. Keep configuration; appended page value normalizes safely on an older build. No character collection or inventory data is persisted or exported, and no outbound service is contacted.
