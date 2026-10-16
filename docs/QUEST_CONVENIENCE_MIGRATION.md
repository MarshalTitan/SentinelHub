# Quest Convenience proving — Hub 0.3.0.0 / HUD 0.8.4.7

> Historical replacement procedure for HUD 0.8.4.6/0.8.4.7. With HUD 0.8.5.0, utility engines have moved to Hub and disabling Hub leaves their actions off. Use [current cleanup acceptance and rollback](UTILITY_CLEANUP.md). Scoped replacement acceptance is recorded in the ecosystem test matrix.

Update both through the existing Sentinel catalog. Anti-AFK's reported four-step PASS is retained; no repeated idle test is requested. Its engine/preferences are unchanged. Quest Convenience is a separate module, default OFF with dialogue/cutscene flags false and rewards Manual for new installs and upgrades.

Open /shub → Utilities → Quest Convenience. Configure the features and enable the module, or explicitly import HUD quest preferences. Import copies only the public dialogue/cutscene/reward settings; it enables the module only if an imported feature is active. It does not change either plugin's Anti-AFK setting or HUD's saved preferences.

While the master is enabled, Hub owns all three quest features, including options Off/Manual. A loaded HUD 0.8.4.7+ must acknowledge ownership before Hub may act. Legacy/unknown installed HUD blocks Hub, including a disabled legacy install. HUD keeps its original engine/settings until replacement acceptance. Missing Hub preserves HUD behavior.

The accepted native algorithm and pure policies are copied with namespace/configuration adaptation only and checked against pinned accepted HUD source in CI. Cutscene skip confirmation remains limited to a request-owned, live AgentCutscene addon ID and base AtkUnitBase callback; no generic Yes/No, remembered addon pointers, choice auto-selection or input synthesis. Reward behavior retains Manual/First/Current Job/Allagan Piece and the existing first-reward fallbacks.

All work is synchronous. Ownership loss, logout, disposal and option changes clear pending cutscene/reward work. Native failures clear the session and retry no faster than once per second. HUD's request guard covers startup/hot reload; claims are private in-memory values, expire after 3 seconds and cannot be released by a stale token. Export adds only a typed module state, excluding native status strings, reward names, choices, preferences and claim identity.

## Short supervised gate

1. Confirm Hub 0.3.0.0 / HUD 0.8.4.7. Quest starts OFF; your accepted Anti-AFK setting remains as saved.
2. Open /shub → Utilities. Configure or import quest preferences; enable Quest Convenience. Expect Hub Active and HUD's quest runtime Managed by Sentinel Hub. Turning the module OFF must restore HUD's retained preferences; Anti-AFK remains independent.
3. With Skip ordinary dialogue on, try an ordinary conversation and a conversation choice. Ordinary text advances; choices and unrelated Yes/No prompts remain manual.
4. Enable Skip cutscenes. Try a skippable cutscene; its dedicated skip confirmation should complete without a crash. Check the FC submarine interaction when naturally available, since its accepted handling must remain intact. A non-skippable scene or unrelated prompt must not be confirmed.
5. Confirm rewards Manual leaves a choose-one reward untouched. On a naturally available choose-one quest reward, test your desired automatic mode; it must select according to that mode then Complete once. Current Job and Allagan Piece retain the existing first-reward fallback. Report which mode you tested; do not claim untested modes or guaranteed-only rewards as live-proven.
6. With Hub owning quests, reload HUD and then Hub; verify one owner, no duplicate actions and correct return to retained HUD preferences when Hub's module is OFF. Logout/login should clear pending work and resume only the saved opt-in. /shub export contains no quest text/reward names/preferences/token.

Return PASS/FAIL plus any untested scenario and the reward mode tested. A screenshot/log and time are needed only for failures. Automated policy/source/render checks do not establish native callback correctness. HUD cleanup remains separate until replacement evidence passes.

Rollback: turn OFF Hub Quest Convenience, or disable Hub. HUD retains its original quest settings and engine. Turning only Quest Convenience OFF leaves Hub Anti-AFK independent. No catalog downgrade or config rewrite is needed. Latency/Weaving remains inactive; camera, visual HUD, Profiles, hunt and PvP behavior are unchanged.
