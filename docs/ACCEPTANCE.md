# Sentinel Hub 0.1.0.0 supervised acceptance

Install through the permanent Sentinel catalog after registration, or use the exact SentinelHub.zip from release v0.1.0.0. There is no separately installed Core plugin. Open with /shub.

1. Dashboard/Plugins: compare installed versions and loaded states with Dalamud. Disable one optional Sentinel plugin, refresh and verify its settings action disables; missing plugins must say Not installed. No automation starts.
2. Dependencies: compare vnavmesh enabled/mesh ready, Lifestream, RSR and Splatoon response (geometry readiness remains unknown). RSR Off and idle Lifestream still say Responding. Missing companions do not turn unrelated plugins faulty.
3. Open settings for each installed consumer. PvP must open configuration, Classy settings (not launcher). Existing HUD/Profiles/Relay command toggling is expected; use a closed window. Open PvP diagnostics.
4. Profiles: unavailable selection/last-applied/drift must remain Unknown. Verify profile behavior is unchanged in Profiles.
5. Resize to the minimum (480 by 500 logical pixels), then taller/wider; visit every page at your UI scale. All eight rail icons, wrapped text, scroll regions and buttons remain reachable. Minimize/restore keeps Modern header/theme, location and expanded size. Switch Classic/Modern; reload Hub and check placement, theme and reduced motion. Check keyboard/controller navigation.
6. Run /shub export; inspect the JSON in Hub's configuration diagnostics folder. It must contain known IDs/versions/status/fault codes only, with no chat, character names, tokens, webhooks or private config. Failures must not close Hub.
7. Disable/uninstall Hub. Open and use the existing Sentinel plugins normally; HUD utility settings and behavior must remain unchanged. Re-enable Hub.

Return PASS/FAIL plus the failing step and screenshot or exact symptom only if needed. Keep the exported Hub JSON for a status issue. Do not run hunt/Frontline automation just to validate this dashboard.

Rollback: disable or uninstall SentinelHub. No consumer migration or cleanup is required.

Recommended next milestone after acceptance: default-OFF Anti-AFK module with tested single-owner transition; do not remove HUD's implementation before the replacement passes supervised testing.
