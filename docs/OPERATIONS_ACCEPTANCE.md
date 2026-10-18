# Hub 0.7.0.0 — consolidated supervised gate

Status: **pending on the latest candidate**. [PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15) links its exact successful CI artifact. Public Hub/catalog remain 0.6.0.0; earlier dev artifacts are superseded.

Observed on the earlier candidate: the user confirmed Quest Convenience Off persisted when switching dev 0.7 to live 0.6. Screenshots showed the obsolete HUD waits and prompted the current cleanup. This is scoped preference evidence, not a PASS for the full gate. Barding, settings backups and global pause are now removed; they need no acceptance tests.

## Install

Download the latest `SentinelHub-0.7.0.0` Actions artifact linked in the PR, extract the outer archive and inner `latest.zip` to an isolated dev folder. Disable live Hub before loading the candidate DLL using the existing development-plugin setup. Confirm 0.7.0.0; keep the usual consumers. Only one Hub copy may run. Core/UI remain 0.4.1 and Dalamud API 15. The public custom-repository URL is unchanged.

## Checklist

1. Dashboard/Utilities: global pause is gone; Backups navigation and Inspector barding are gone. Resize Plugins narrow → wide → narrow: actions move right when space allows and stack below when narrow, with readable text/buttons. Minimize/restore still works.
2. With current HUD 0.8.5.2, enable Anti-AFK: it becomes Active without a HUD acknowledgment wait and its successful-check counter advances after roughly 10 seconds. Off stops increments; repeat after a Hub reload. Quest Convenience also has no HUD wait. Confirm its saved Off switch remains Off after reload; exercise only your normally used options.
3. Health: compare installed/live/dev versions with Dalamud; fetch dated public versions. Check SRank ready/enabled status while your character is loaded. Unsupported endpoints remain Unknown/Unavailable. If practical, stop dependent automation and disable/re-enable a safe dependency; verify recovery/activity without spam.
4. Export via Diagnostics or `/shub export`: ZIP has report.json, hub-state.json, summary.txt, useful versions/states and no credentials, names/IDs, chat or config documents. If an overlapping reviewed plugin is already installed, check Compatibility's clearly scoped Hub suspension/recovery; extra installations are not required.
5. With Inspector off, identify a mounted player and a player with an active fashion accessory. Correct name/icon and collection entry must still resolve. Check none-active case, own unlock and existing acquisition/source details.
6. Enable shared Player Inspector. With Hub closed/minimized, inspect a loaded player performing a supported loop such as `/lean`: verify name/icon/command, your own unlock and attributed source/Unknown fields. When they stop/move, show None; unloading/zoning/Off clears the observation. Idle/pose/combat must not invent an emote.
7. Recent Activity: verify health/compatibility/export results, grouped repeats, filters and persistence across a reload. Absent provider contracts need not generate events. Other Hub utilities and consumer windows remain usable.

Report PASS/FAIL plus untested cases. Automated checks do not establish live game replication. Keep the PR held until required observed checks pass; record unsupported scenarios honestly.

## Rollback

Turn Hub utility switches Off, unload the candidate, then re-enable live 0.6.0.0. Preserve settings. Each consumer retains its own controls. No configuration deletion, restore or automatic consumer restart is needed.
