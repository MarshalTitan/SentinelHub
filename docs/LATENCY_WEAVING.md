# Latency / Weaving proving — Hub 0.4.0.0

[Installable release](https://github.com/MarshalTitan/SentinelHub/releases/tag/v0.4.0.0). Update Hub through the existing catalog; HUD stays 0.8.5.1. Open /shub → Utilities → Latency / Weaving.

This is an independent instant-action compensation implementation, not all NoClippy features. Mode defaults **Off**, including upgrades. **Measure only** observes without writing; **Compensate** adjusts the local remaining animation lock after a matched response. It does not choose/use actions, change cooldowns, rewrite packets, alter cast locks or predict statuses.

## Short in-game gate

1. With the existing NoClippy still loaded, choose Measure only: expect **Blocked: NoClippy** and no increasing sample/adjustment counts. Set Off, then disable NoClippy. XivAlexander, if present, must also be disabled/unloaded; there is no conflict override.
2. In a safe area at a training dummy, disable external rotation automation for this test. Choose Measure only and manually use about ten instant weaponskills/abilities or instant spells. Expect Measuring, increasing samples and sensible response values, with **zero applied adjustments**. Cast-time spells, items and long/special locks should not produce adjustments.
3. Choose Compensate. Manually use your normal single/double weave for about a minute. If measured response is above 40 ms, applied counts should increase; a response at or below the fixed buffer correctly needs no correction. Actions/combos/recasts should remain normal. Report any clipping, failed action, animation oddity or crash; do not treat the counter alone as proof of smoother gameplay.
4. Set Off: new samples/adjustments stop and normal play continues. Re-enable your chosen mode, zone once, and reload Hub once: no stale correction, crash or duplicate owner. With Hub Off, you can restore NoClippy.

Return PASS/FAIL with Hub version, job, approximate response delay, whether counts increased, and whether single/double weaving felt better, unchanged or worse. State any untested step. The prior utility and inventory tests are separate; do not repeat their full checklists.

If Measure only records no eligible samples, do not proceed to Compensate. Report job and displayed state. A stopped Contested/Faulted/Unsupported module requires Off before retrying. Rollback: set Off or disable Hub; [Hub 0.3.1.1](https://github.com/MarshalTitan/SentinelHub/releases/tag/v0.3.1.1) remains available. Existing utility preferences remain saved.

## Bounds and lifecycle

Only a successful native instant-action request with a new nonzero sequence and the standard 0.5-second predicted lock is tracked. Game-data categories Spell/Weaponskill/Ability with natural cast time zero are eligible; natural cast-time actions remain excluded even under instant-cast effects. Only the current local actor's matching action ID/sequence, still the latest native request, can consume it. The pending slot expires after 500 ms and is cleared on mode/territory/lifecycle changes. No pointers or delayed native work are retained.

The response must have an unchanged normal 0.5–0.8-second server lock in 10 ms increments, an intact post-handler lock, and a plausible pre-handler countdown. Correction is min(max(measured response − 40 ms, 0), 250 ms); it changes only the local remaining lock, retaining the intrinsic duration over elapsed time and a positive remaining bound. Late, casted, stale, reordered, duplicate, malformed and unsupported samples do not adjust. Unexpected modified locks stop the session. Off/logout/transition/death/conflict disposes both hooks; native setup/contention faults latch until Off.

All original native calls and arguments are forwarded exactly once, including when observations fail. No packet/network-send hook is installed. Known NoClippy copies are checked through public loaded-state discovery; XivAlexander detection checks loaded module names and identifying exports, including renamed proxies. Full module checks run before an eligible response can write. These checks cannot identify every custom third-party modification.

## Source audit

- NoClippy reference: [3f4b37739bbdccd1833b042018b01be84a7d382b](https://github.com/UnknownX7/NoClippy/tree/3f4b37739bbdccd1833b042018b01be84a7d382b), Game.cs and AnimationLock.cs. Reviewed for behavior/signatures; no implementation files copied.
- Current typed bindings: [FFXIVClientStructs 994319b521c85e445e8907474b3a905cc05187c3](https://github.com/aers/FFXIVClientStructs/tree/994319b521c85e445e8907474b3a905cc05187c3), ActionManager/ActionEffectHandler. The build compiles against current Dalamud development assemblies.
- Action schema: [83e965d091116f895d5b17573cc5d12909a5f407](https://github.com/xivdev/EXDSchema/blob/83e965d091116f895d5b17573cc5d12909a5f407/Action.yml), Cast100ms and ActionCategory; managed metadata is cached on first enable.
- XivAlexander identifying exports/names: [2c71d109c7738d5ef47b291f8df879b251265c26](https://github.com/Soreepeong/XivAlexander/tree/2c71d109c7738d5ef47b291f8df879b251265c26), Exports_x64.def, loader Exports64.def and XivAlexander.h.

Automated policy/lifecycle/original-forwarding/render/package checks do not establish native game correctness, actual conflict detection on this PC, response measurement accuracy or a felt improvement. This release is default-off proving availability pending the gate above.
