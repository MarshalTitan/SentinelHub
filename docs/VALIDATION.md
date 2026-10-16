# Automated validation — Sentinel Hub 0.2.0.0

Migration source review: [PR #2](https://github.com/MarshalTitan/SentinelHub/pull/2), head `34e382299353297e8441ab8660e77cab4de08f38`.
Cloud validation: [38849930385](https://github.com/MarshalTitan/SentinelHub/actions/runs/38849930385), completed successfully.

- **27 policy groups PASS:** inventory/quick-open/persistence/privacy regression coverage; default-OFF upgrades, login/legacy-HUD/acknowledgment gates, disabled-state no-write behavior, 10-second rate limit, finite 30-second threshold, bounded timer faults, explicit preference persistence and token exclusion.
- The test compiles the actual HUD lease source pinned to `c86e688d747757e5c6d50aa95a44ce12bed53776` and simulates initial callback orders, handoff, expiration and replacement. This is policy evidence, not a live IPC claim.
- **480 native page/state/width/theme/scale cases plus 42 Anti-AFK state/theme/scale cases PASS.** Keyboard/controller appearance activation, minimize/restore and balanced stacks passed.
- Compilation: **zero warnings/errors**. PowerShell publication scripts parsed.
- Exact Core/UI **0.4.1** hashes/locks and bundled DLLs verified. Installable ZIP identity/API/version/paths and installed IconUrl/RepoUrl passed; no consumer assemblies are bundled.
- Direct audit of the public **0.1.0.0 ZIP** confirmed its installed manifest was missing IconUrl. The public replacement icon passed PNG signature validation.
- HUD compatibility [PR #5](https://github.com/MarshalTitan/SentinelHUD/pull/5), head `c86e688d747757e5c6d50aa95a44ce12bed53776`, passed [build/tests](https://github.com/MarshalTitan/SentinelHUD/actions/runs/38849521851) and [release infrastructure checks](https://github.com/MarshalTitan/SentinelHUD/actions/runs/38849521862).

The user reported the **0.1.0.0 dashboard looks good**, authorized retiring Sentinel Hunts from Hub and continuing migration, and reported the missing image. This is visual dashboard evidence; it does not certify every original MVP checklist step.

**FFXIV Anti-AFK acceptance pending.** Native rendering/policy tests do not establish real timer behavior or IPC/plugin lifecycle. Follow [the supervised procedure](ANTI_AFK_MIGRATION.md). HUD cleanup remains gated; disabling Hub returns to HUD's retained preference. Public release and catalog verification must pass before the new versions are advertised as available.

Historical MVP validation/publication: [0.1.0.0 release run](https://github.com/MarshalTitan/SentinelHub/actions/runs/38846452080), 21 policy groups and 480 native UI cases; public ZIP SHA256 `7ee080adf3cec91e92f4b106aa0720ddf925a023ddb1a258da74a177ed0c0690`.

## Quest Convenience candidate — 0.3.0.0

Anti-AFK received the user's scoped four-step PASS on 2026-10-16 America/Toronto; its source/settings are unchanged here. Quest acceptance remains pending.

Required cloud validation adds complete accepted native/policy source equality, default-OFF upgrade/import/normalization, actual pinned HUD preference/lease interoperability, independent utility ownership, revocation of pending work, bounded faults, contextual prompt identity/timeout and reward rule tests. Native rendering adds 288 Quest state/import/mode/theme/scale cases to the existing 480 page cases and 42 Anti-AFK cases. Exact Core/UI 0.4.1 pins, scripts, build and package checks remain mandatory. Results and publication evidence are recorded after the runs complete; no current candidate count is claimed passed before then.
