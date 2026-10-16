Sentinel Hub 0.3.1.0 updates utility ownership guidance for HUD 0.8.5.0.

- Anti-AFK and Quest engines, options, imports and ownership checks are unchanged.
- HUD 0.8.5.0 removes duplicate engines; turning a module or Hub off now turns its utility actions off.
- Prior compatible HUD builds still resume their retained preferences when Hub releases ownership.
- Saved HUD preferences remain available for explicit import. No automatic enable or reimport.
- Scoped Quest user PASS recorded; reward mode unspecified, submarine not separately confirmed.
- Camera, HUD visuals, Core packages, navigation and other consumers remain unchanged.

Update Hub first, then HUD. Follow [cleanup acceptance and rollback](UTILITY_CLEANUP.md).
