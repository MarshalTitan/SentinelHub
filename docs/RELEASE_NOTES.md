Sentinel Hub 0.2.0.0 begins the staged Anti-AFK migration.

- Removes the retired Sentinel Hunts test component from Hub inventory and dependency uses.
- Adds IconUrl/RepoUrl to the installed plugin manifest and validates the public PNG.
- Adds default-OFF Anti-AFK, preserving HUD's 10-second checks and 30-second threshold without input/movement/chat.
- Requires a fresh ownership acknowledgment from loaded HUD 0.8.4.6+. Legacy/unknown HUD installations block Hub's writer.
- Keeps HUD's saved preference and implementation. Import only on explicit click. Disable/unload Hub to return to the retained HUD preference.
- Quest Convenience and latency remain unchanged/inactive in Hub.

This is a proving release. Supervised native behavior and plugin reload/ownership acceptance are required before HUD cleanup. No cleanup/removal is included.
