# Hub 0.7.0.0 — supervised acceptance record

Status: **focused release gate PASS — 2026-10-18 16:13 America/Toronto**. The user confirmed the final supplied build from source `2a02f29aa48cd5c59b5117e80a0d42cc7489f220`: Anti-AFK is good and its extra text is gone; Activity works after reload; card spacing is better when wide and narrow. This closes the remaining focused gate in [PR #15](https://github.com/MarshalTitan/SentinelHub/pull/15). Release preparation below changes documentation only; product code remains the accepted implementation.

Software evidence: [CI 39161131977](https://github.com/MarshalTitan/SentinelHub/actions/runs/39161131977), 107 policy groups / 1990 native UI cases, typed ornament/emote fixtures and exact package/Core validation. Final release build/distribution evidence is tracked in the PR and canonical ecosystem documents.

Observed on earlier candidates: Quest Convenience Off persisted across dev→live; pause removal and responsive plugin actions passed; Health looked good. On the e046cde proving build, Anti-AFK reached 46 checks (screenshot 47) and stopped after Off; Inspector Off removed the menu action, On showed accessory and emote; Activity recorded changes. These checks retain that scope; names/icons/own unlock/source and every stale-target case were not separately reported. Barding, backups and global pause are removed.

The final changes simplify Anti-AFK presentation, make Activity session-only and tighten cards. Both focused checks below have now passed as reported. Earlier observed behavior retains its recorded scope.

## Historical proving installation

The accepted proving procedure used the latest `SentinelHub-0.7.0.0` Actions artifact linked in the PR, extract the outer archive and inner `latest.zip` to an isolated dev folder. Disable live Hub before loading the candidate DLL using the existing development-plugin setup. Confirm 0.7.0.0; keep the usual consumers. Only one Hub copy may run. Core/UI remain 0.4.1 and Dalamud API 15. The public custom-repository URL is unchanged.

## Checklist

1. Dashboard/Plugins/Profiles cards have reduced unused space beneath their content at both expanded and narrow widths, with all text/buttons reachable. Utilities shows only the Anti-AFK heading and switch, with Active/Off (or an honest unavailable/waiting state) in the label. No counter or explanatory paragraphs remain underneath. Switch behavior/settings remain as already tested.
2. Recent Activity records changes during the session, then starts fresh after a Hub reload/game restart. Existing settings remain saved. The old candidate's exact recent-activity.json is removed automatically; no replacement history file is written.

Optional troubleshooting check: Diagnostics explains that normal history is RAM-only. If you choose Export, it saves diagnostics/sentinel-diagnostics.zip with report.json, hub-state.json and summary.txt. A second export replaces that one file; exports are deliberate saved files and remain after closing the game. No export is needed for normal operation. Previously created manual ZIPs are not automatically deleted.

Remaining unreported scenarios stay explicit: Quest's refreshed no-HUD-wait behavior, broader collection/Inspector own-unlock/source/stale-target cases, and optional provider recovery/compatibility. Use these when available; do not install extra providers solely for acceptance. Prior scoped Mount/Fashion/utility acceptance remains recorded.

The focused release gate is closed. Unreported scenarios above remain unreported; this is not blanket certification of every provider, acquisition field or invalid-actor scenario. Automated checks and scoped human evidence are recorded separately.

## Rollback

Turn Hub utility switches Off, unload the candidate, then re-enable live 0.6.0.0. Preserve settings. Each consumer retains its own controls. No configuration deletion, restore or automatic consumer restart is needed.
