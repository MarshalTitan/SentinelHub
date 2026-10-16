Sentinel Hub 0.3.0.0 adds opt-in Quest Convenience proving.

- Default-OFF module with ordinary dialogue, cutscene skipping and Manual/First/Current Job/Allagan Piece reward modes.
- Explicit import copies HUD's three public quest preferences only; original HUD settings remain saved.
- Requires HUD 0.8.4.7+ ownership acknowledgment when loaded; installed legacy/unknown HUD blocks actions.
- Preserves accepted HUD native/policy source, including the submarine/base-addon cutscene fix and manual choices.
- Ownership loss, option changes, logout and unload clear pending work. No delayed native actions survive a handoff.
- Anti-AFK's reported acceptance is retained and its implementation/settings are unchanged.
- HUD keeps its fallback engine pending supervised replacement acceptance. Latency/Weaving remains inactive.

Follow docs/QUEST_CONVENIENCE_MIGRATION.md for the exact test and rollback. This proving publication does not certify native gameplay behavior.
