# Sentinel Hub 0.9.0.0

- Adds Death Recaps: 10/30/60-second HP/shield/status review, bounded character-local history, privacy and Modern 2 UI.
- Adds Frontline targeter-job breakdowns, maximum focus, freshness and intervals using HUD 0.8.9.0's existing scan. Targeters remain distinct from damage sources; HUD stays optional.
- Adds a compact independent death popup and synthetic PvE/PvP previews that are never saved.
- Optional experimental incoming action-effect damage/healing capture is off by default. Exact fatal attribution, pre-hit HP/shield HP and overkill remain Unknown/Unavailable.
- Preserves Treasure Maps and prior performance fixes. No extra enemy targeting scans, live target advice, movement/combat actions, telemetry or uploads.

Automated policy, packet and native-UI fixtures run alongside existing regressions. Native game attribution, Frontline observations and movement acceptance remain explicitly pending; see docs/DEATH_RECAPS.md. Core/UI stay pinned at 0.4.1.
