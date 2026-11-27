# Navigation polish - 0.9.0.5

Mouse activation now clears rounded focus/hover decoration after pointer exit; keyboard/controller focus keeps its visible cue. Native focus and button callbacks are unchanged.

Uses compatible SentinelCore UI 0.4.1.3, source `0d59460cc592f1589600e11e4e9e04478667b629`. Existing Modern sidebar icons have borderless idle states, persistent rounded selection, soft animated hover highlights and padded translucent tooltip bubbles. Custom minimize/restore controls use drawn down/up chevrons; close uses a more visible X. Native hit areas, keyboard/controller focus, window dragging, saved placement and callbacks remain intact. Headers follow the selected page. Classic and plugin functionality are preserved. Compact single-page Treasure Queue and Death Recap windows suppress redundant context captions.

Dependency packages are restored from the immutable release and SHA-256 checked before locked restore. Locks record the release SHA-512 values. Automated builds/native UI/persistence checks are recorded in the coordinated Sentinel report. Actual FFXIV visual acceptance remains separate from native renderer checks.

---

# 0.9.0.2

Remove retired projects from supported inventory, quick-open actions and dedicated dependency checks. All supported plugin behavior and saved settings are preserved.

# 0.9.0.1

Privacy maintenance: bundle Core 0.4.1.1 with project copyright attribution. Runtime source, configuration, and gameplay behavior are unchanged.

# Sentinel Hub 0.9.0.0

- Adds Death Recaps: 10/30/60-second HP/shield/status review, bounded character-local history, privacy and Modern 2 UI.
- Adds Frontline targeter-job breakdowns, maximum focus, freshness and intervals using HUD 0.8.9.0's existing scan. Targeters remain distinct from damage sources; HUD stays optional.
- Adds a compact independent death popup and synthetic PvE/PvP previews that are never saved.
- Optional experimental incoming action-effect damage/healing capture is off by default. Exact fatal attribution, pre-hit HP/shield HP and overkill remain Unknown/Unavailable.
- Preserves Treasure Maps and prior performance fixes. No extra enemy targeting scans, live target advice, movement/combat actions, telemetry or uploads.

Automated policy, packet and native-UI fixtures run alongside existing regressions. Native game attribution, Frontline observations and movement acceptance remain explicitly pending; see docs/DEATH_RECAPS.md. Core/UI stay pinned at 0.4.1.
