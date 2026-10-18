# Hub 0.7.0.0 — one consolidated supervised gate

Status: pending. Public Hub/catalog remain 0.6.0.0. [Integration PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15) is held until this gate passes. Use the final exact-tree CI artifact linked in the PR; earlier phase artifacts are superseded.

## Load the proving candidate

Download the `SentinelHub-0.7.0.0` artifact from the final successful PR workflow. Extract the artifact archive, then extract its inner `latest.zip` to an isolated candidate folder. Disable live Hub before loading that folder's SentinelHub.dll through your existing Dalamud development-plugin setup. Confirm Hub **0.7.0.0**; keep the usual Sentinel consumers. Do not load two Hub copies simultaneously. This does not change the central custom-repo URL or publish a public Hub version.

The candidate uses current Core/UI 0.4.1 and Dalamud API 15. A UI layout/IPC/export/backup proof can use the same build as inspection/pause proof. Keep automation idle for dependency and restore checks. Current SRank/PvP do not acknowledge global pause: use their own stop controls; the Hub page must say Unsupported/partial pause rather than stopped.

## Checklist

1. Health: compare installed/live/dev versions with Dalamud. Fetch public versions; verify the dated comparison. For your loaded SRank character, check ready/enabled status; other unsupported contracts remain clearly unknown.
2. Dependency recovery: with dependent automation stopped, disable/re-enable a safe dependency. Health/activity should distinguish not loaded, IPC response and readiness, then recovery without repeated spam.
3. Export: `/shub export` or Diagnostics → Export Sentinel Diagnostics creates a ZIP containing report.json, hub-state.json, summary.txt. Confirm useful versions/states, and no tokens/webhooks/character names/IDs/chat/config contents. Send this package if a check fails.
4. Compatibility: if a reviewed overlapping provider is already present, verify the shown definite/possible/informational status and affected Hub suspension/recovery. Hub must not change that provider. Mark unavailable provider scenarios untested; installing extra plugins is not required.
5. Existing Mount lookup still resolves the right mount/name/icon and opens its collection entry. Existing source/layout/settings remain intact.
6. Existing Fashion lookup still resolves the accessory and opens its entry. Mount/Fashion-only opt-ins still work with broader Inspector off.
7. Inspector: enable shared Player Inspector. With Hub closed/minimized, right-click another player → Sentinel Hub: Inspect player. The page opens and refreshes safely; no previous player's result is retained after a fresh failed lookup, despawn, zone or Off.
8. Barding: inspect an owner with a summoned chocobo, ideally mixed head/body/leg barding. Compare all three names/icons and your own unlock state/source. Also try Company Chocobo mounted if practical. No loaded companion, ambiguous match or other mount must not produce invented barding.
9. Emote: inspect someone performing `/lean` or another supported loop. Verify command/name/icon/own unlock/source; have them stop/move and verify None. Combat/idle/pose and short one-shot states must not become guessed emotes.
10. Pause/resume: enable the Hub utilities you normally use, request pause and verify their paused states/no pending actions. Resume retains saved choices and other conflict/readiness gates. Current SRank/PvP must explicitly stay Unsupported, not falsely acknowledged.
11. While paused, verify HUD visuals, settings, collections/inspection, Profiles management and Relay receiving remain usable.
12. Recent Activity: verify useful health/compatibility/pause/export events, severity/filtering, grouped repeats and persistence across one Hub reload. Unsupported provider events need not appear.
13. Backup: create a named backup. Check included owners/fields and exclusions. It must omit missing/unreviewed owners and all Relay character/credential data. This is selective settings backup, not full profiles/gear-set/channel migration.
14. Restore proof: keep external automation idle. Create a test backup, change only a benign Hub preference (such as reduced motion/theme), pause Hub, select that backup and **SentinelHub** owner, confirm restore. Verify the preference restores, Hub stays paused and a pre-restore backup exists. External restore must reject a loaded owner/newer or unreviewed schema/build. Do not restore an external owner unless you deliberately unloaded it and keep it unloaded until completion.

Check minimum width, both themes you use and minimize/restore while visiting the new pages. Report PASS/FAIL, Hub version/theme and untested scenarios. A PASS for one inspected actor does not establish all cases. No public release until required observed checks pass; unsupported/untested cases retain their explicit scope.

## Rollback

Turn Hub utility preferences Off while paused, unload the candidate, then re-enable live 0.6.0.0. Preserve configuration/backups. Current nonparticipating consumers retain their own stop/resume controls. Do not delete configs or automatically restart other plugins.
