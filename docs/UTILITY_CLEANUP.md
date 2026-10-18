# Completed utility transfer UI — Hub 0.4.0.1

On 2026-10-17 the user confirmed the second tester installed Hub and copied preferences.
Utilities no longer exposes migration imports or saved-HUD summaries in either theme.
Serialized settings and historical import helpers remain compatible; no saved preferences
are cleared and no automatic import or opt-in occurs. Ownership/legacy safeguards and
native engines remain byte-for-byte unchanged.

HUD 0.8.5.2 separately removes its retired Convenience page. Either UI update may arrive
first; retained v1 bridges keep existing combinations working. No new mandatory dependency.

Acceptance: check Hub Utilities in Modern and Classic, verify existing switches/reward mode,
and absence of import buttons/transfer guidance. No repeat of accepted gameplay is required.
Report changes only; utility Off is the immediate rollback. Keep the prior immutable release.


## Held 0.7.0.0 follow-up

Current HUD 0.8.5.0+ contains no native utility engines, so the operations candidate no longer requires its obsolete ownership acknowledgment. Old/unknown engine-bearing builds retain version/exclusive-handoff safeguards; selection checks all HUD installations so a disabled modern entry cannot hide an older engine. No saved utility preference is changed. Native Anti-AFK checks stay every 10 seconds; ownership polling is bounded to one second with immediate Off/login handling. See [current proving](OPERATIONS_ACCEPTANCE.md).
