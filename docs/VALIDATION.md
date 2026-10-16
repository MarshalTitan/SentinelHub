# Current validation — 0.3.1.0 / HUD 0.8.5.0

The latest Quest checklist received scoped user PASS. Reward mode was not named; submarine was not separately confirmed. The engineering phase continues with separate HUD utility cleanup. Hub native algorithms, controllers, configuration and Core/UI 0.4.1 pins are unchanged; the current Hub change updates UI guidance and release metadata. HUD removes the two native engines/call sites, retains schema 12/preferences/v1 IPC and keeps Core/UI 0.3.1.

Cloud build/policy/native-UI/package checks must pass before publication. HUD also tests all 32 combinations of original utility preferences through schema normalization/serialization and compatibility lease callbacks without changing visual/camera/window values. Publication does not close the [cleanup in-game gate](UTILITY_CLEANUP.md).

## Historical Quest proving validation (0.3.0.0)


Initial Quest source [39318a4bf3c8c3adb0e36612ceb22aacf36b974b](https://github.com/MarshalTitan/SentinelHub/commit/39318a4bf3c8c3adb0e36612ceb22aacf36b974b) passed [cloud run 38852926583](https://github.com/MarshalTitan/SentinelHub/actions/runs/38852926583). The About version correction follows separately; final main publication repeats validation before upload.

- 35 policy groups PASS: retained inventory/command/privacy/Anti-AFK coverage plus default-OFF Quest upgrades, explicit three-preference import, normalization, legacy/login/acknowledgment gates, pending-work revocation on loss/options/faults, bounded retries, actual pinned HUD wire/independent-lease compatibility, contextual cutscene identity/deadline and reward modes/fallbacks.
- 810 native UI cases PASS: 480 existing page/state/width/theme/scale cases, 42 Anti-AFK cases, 288 Quest state/import/reward-mode/theme/scale cases. Existing keyboard/controller, collapse/restore and balanced-stack checks passed.
- Exact complete native/policy source comparison against accepted HUD 758068f68a76d72660fb6e2740f22d592130fdd7 PASS; namespace/config adaptation only. This preserves the base-addon/live-ID submarine fix; no new gameplay algorithm is asserted tested in FFXIV.
- Compilation: zero warnings/errors. PowerShell parsing, exact Core/UI 0.4.1 hashes/locks and bundled bytes, identity/API/version and installed icon/repository metadata PASS.
- HUD compatibility PR #6 at bcc21e44c83fac60c14b1318eddc0a58d7ab4bf9 passed build/test 38852669218 and release infrastructure 38852669157. It retains engine/settings/schema; new public handoff clears pending work before acknowledgment.

The user reported PASS on 2026-10-16 America/Toronto for the latest four-step Anti-AFK checklist: Hub Active/HUD Managed, return to retained HUD setting, Hub reload and idle beyond the configured interval. Installed versions were not restated in that reply; the preceding instruction identified Hub 0.2.0.0 / HUD 0.8.4.6. This is scoped human evidence, not independent native inspection or blanket acceptance of all earlier scenarios. Anti-AFK is unchanged in this build.

**At publication, Quest native acceptance was pending; the later scoped user PASS supersedes that status.** Follow [the exact procedure and rollback](QUEST_CONVENIENCE_MIGRATION.md). Report any untested case and the reward mode exercised; policy/render/source checks do not prove native callbacks, hot reload or live rewards. HUD cleanup remains separate.

Historical Anti-AFK/MVP validation: 27 policy groups and 522 native UI cases for 0.2.0.0; 21/480 for 0.1.0.0. Their original source/publication evidence remains in the central ecosystem records. No earlier unrelated gameplay gate is advanced by this release.
