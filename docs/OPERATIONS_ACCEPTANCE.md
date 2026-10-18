# Hub 0.7.0.0 — consolidated supervised gate

Status: **pending on the latest candidate**. [PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15) links its exact successful CI artifact. Public Hub/catalog remain 0.6.0.0; earlier dev artifacts are superseded.

Observed on earlier candidates: Quest Convenience Off persisted across dev→live; pause removal and responsive plugin actions passed; Health looked good. On the e046cde proving build, Anti-AFK reached 46 checks (screenshot 47) and stopped after Off; Inspector Off removed the menu action, On showed accessory and emote; Activity recorded changes. These checks retain that scope; names/icons/own unlock/source and every stale-target case were not separately reported. Barding, backups and global pause are removed.

The latest change simplifies Anti-AFK presentation and makes Activity session-only. The new build needs the two focused checks below; already reported behavior does not require a full repeated gameplay test.

## Install

Download the latest `SentinelHub-0.7.0.0` Actions artifact linked in the PR, extract the outer archive and inner `latest.zip` to an isolated dev folder. Disable live Hub before loading the candidate DLL using the existing development-plugin setup. Confirm 0.7.0.0; keep the usual consumers. Only one Hub copy may run. Core/UI remain 0.4.1 and Dalamud API 15. The public custom-repository URL is unchanged.

## Checklist

1. Dashboard/Plugins/Profiles cards have reduced unused space beneath their content at both expanded and narrow widths, with all text/buttons reachable. Utilities shows only the Anti-AFK heading and switch, with Active/Off (or an honest unavailable/waiting state) in the label. No counter or explanatory paragraphs remain underneath. Switch behavior/settings remain as already tested.
2. Recent Activity records changes during the session, then starts fresh after a Hub reload/game restart. Existing settings remain saved. The old candidate's exact recent-activity.json is removed automatically; no replacement history file is written.

Optional troubleshooting check: Diagnostics explains that normal history is RAM-only. If you choose Export, it saves diagnostics/sentinel-diagnostics.zip with report.json, hub-state.json and summary.txt. A second export replaces that one file; exports are deliberate saved files and remain after closing the game. No export is needed for normal operation. Previously created manual ZIPs are not automatically deleted.

Remaining unreported scenarios stay explicit: Quest's refreshed no-HUD-wait behavior, broader collection/Inspector own-unlock/source/stale-target cases, and optional provider recovery/compatibility. Use these when available; do not install extra providers solely for acceptance. Prior scoped Mount/Fashion/utility acceptance remains recorded.

Report PASS/FAIL plus untested cases. Automated checks do not establish live game replication. Keep the PR held until required observed checks pass; record unsupported scenarios honestly.

## Rollback

Turn Hub utility switches Off, unload the candidate, then re-enable live 0.6.0.0. Preserve settings. Each consumer retains its own controls. No configuration deletion, restore or automatic consumer restart is needed.
