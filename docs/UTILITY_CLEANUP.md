# Utility cleanup acceptance

Builds: [Hub 0.3.1.0](https://github.com/MarshalTitan/SentinelHub/releases/tag/v0.3.1.0) and [HUD 0.8.5.0](https://github.com/MarshalTitan/SentinelHUD/releases/tag/v0.8.5.0). Update Hub first, then HUD through Dalamud; confirm both versions. Open `/shub` → Utilities and `/shud` → Systems → Questing / Convenience (Classic: Convenience).

1. Confirm your Hub Anti-AFK/Quest options and HUD camera/layout are retained. HUD's Convenience page must show the migration notice and saved preferences, with no utility switches.
2. With your chosen Hub utilities enabled, expect Hub Active. Exercise one ordinary dialogue and a choice: text advances; the response stays yours. No repeat of the previous full idle/cutscene/reward checklist is required for this structural cleanup.
3. Turn both Hub utilities OFF while leaving HUD loaded. Ordinary dialogue must remain manual. Wait at least 35 seconds, then reopen Hub: its successful Anti-AFK check count must not increase while Off. This counter checks Hub inactivity only; source removal/CI establishes absence of HUD's writer.
4. Disable Hub in Dalamud. HUD and camera must still work; utility actions remain off. Re-enable Hub, restore your desired utility opt-ins and reload HUD once: one active Hub owner, original settings intact, no error/crash.

Return PASS, or FAIL with step, versions and symptom. The earlier Quest PASS did not name the reward mode or separately confirm submarine; this gate does not retroactively certify those cases. No additional reward-mode test is required for unchanged Hub native algorithms.

Rollback: turn both Hub modules OFF before downgrading HUD to [0.8.4.7](https://github.com/MarshalTitan/SentinelHUD/releases/tag/v0.8.4.7). This version can run its retained old preferences; review them before resuming. Hub [0.3.0.0](https://github.com/MarshalTitan/SentinelHub/releases/tag/v0.3.0.0) remains available if needed. Do not delete configuration or enable two incompatible owners. Latest Hub works with the prior compatible HUD.

This changes utility ownership only. Existing Core pins, HUD visuals/camera and unrelated consumers retain their own gates.
