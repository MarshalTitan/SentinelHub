# Sentinel Hub 0.7.0.0

Add Health/Dependency Doctor, an optional sanitized diagnostics ZIP, reviewed compatibility self-protection, a shared passive Player Inspector for mounts/accessories/supported loop emotes, and bounded session-only Recent Activity.

- Health separates installed/live/dev/catalog versions, IPC response and readiness; unsupported states remain explicit.
- Diagnostics and Activity stay in memory during normal use. Explicit Export replaces one diagnostics/sentinel-diagnostics.zip. The earlier candidate's exact activity history file is retired; normal settings and user exports remain intact.
- Player Inspector uses current typed actor state, revalidates identity/session/lifetime, and preserves the existing Mount/Fashion workflows. Emotes require explicit matching loop state; poses and arbitrary animations are not guessed. Dated attributed source facts remain identity-gated, with current availability Unknown.
- Plugin cards on Dashboard, Plugins and Profiles use tighter sizing and put actions beside content when space permits, with a narrow fallback. Both themes, resizing and keyboard/controller behavior are preserved.
- Anti-AFK uses a compact state-labeled switch. Current HUD needs no obsolete utility ownership acknowledgment; older engine-bearing HUD builds retain safeguards. Ten-second native timer checks and accepted Quest/Latency engines are unchanged.

The unreleased barding, global pause and settings backup/restore experiments are omitted from this release. No consumer/Core update is required; Core/UI remain exactly 0.4.1. Mount/Fashion layouts, metadata, tracks and token logic remain unchanged.

Validation: 107 policy groups and 1990 native UI cases, typed ornament/emote memory fixtures, input/collapse/stack checks and package/exact Core validation. Scoped native evidence includes active Anti-AFK checks and Off stopping them, Inspector opt-in/recognized accessory/emote, Activity recording/reload behavior, responsive buttons/tighter cards, compact Anti-AFK appearance and Health feedback. The final focused gate passed on 2026-10-18 at 16:13 America/Toronto; unreported scenarios retain their explicit scope.

See [acceptance record](OPERATIONS_ACCEPTANCE.md), [source/API/storage audit](OPERATIONS_EXPANSION.md), and [release PR](https://github.com/MarshalTitan/SentinelHub/pull/15).
