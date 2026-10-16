# Anti-AFK migration gate — Hub 0.2.0.0 / HUD 0.8.4.6

Update both plugins through the existing catalog. Hub Anti-AFK defaults OFF, including upgrades. HUD retains its setting, implementation and schema. No Quest Convenience/submarine/reward, camera, hunt or Frontline behavior is changed.

Open /shub → Utilities. Enable Anti-AFK explicitly or click Import saved HUD preference to copy only that bool. The original HUD preference stays saved for rollback. A loaded compatible HUD must acknowledge the current in-memory session claim before Hub can touch timers. Legacy/unknown HUD installations block Hub, including disabled legacy installs that might be hot-loaded.

The optional v1 IPC uses primitive booleans/ints and a private in-memory token. HUD suspends synchronously, guards initial/hot-reload callback order and expires unrenewed claims after 3 seconds. Hub's native engine is synchronous, bounded to one check per 10 seconds, and resets only finite timers above 30 seconds. No tasks or stale continuations can act after disable. Missing IPC/module or faults produce inactive status. Diagnostics export only enum state/cycle count; no claim token or private config.

## Exact supervised check

1. Confirm Hub 0.2.0.0 and HUD 0.8.4.6; Hub Utilities starts Off. Confirm the installer icon and no Sentinel Hunts in Hub.
2. With HUD's existing preference On, enable/import in Hub. Hub must show Active and HUD must show Managed by Sentinel Hub. Check Hub's successful-check count advances about once per 10 seconds.
3. Disable Hub Anti-AFK: Hub Off; HUD resumes its retained On preference. Re-enable, then disable/reload Hub itself; verify bounded return to HUD. With HUD preference Off, Hub removal must leave Anti-AFK Off.
4. Enable Hub, reload HUD while logged in, then reload Hub. Ownership must resume without two active states. Logout/login must show waiting then restore only the saved opt-in.
5. While Hub owns Anti-AFK, leave the client idle longer than your configured automatic-away interval; automatic-away should not engage. Then turn both preferences Off and leave the client idle for that same interval; ordinary automatic-away should return. The successful-check counter alone does not prove timer resets. Check camera, HUD and normal controls remain unchanged.
6. /shub export: enum/cycle diagnostics only, no token/chat/private config. A loaded compatible HUD with missing IPC must block Hub, as must any installed legacy/unknown HUD. A completely absent HUD permits standalone Hub operation. No need to install an old build just for this test.

Return PASS/FAIL and the failing step. Native timing/IPC behavior remains pending until this evidence returns. Rollback: turn Hub Anti-AFK Off or disable Hub; HUD retains its original preference. Do not remove HUD's implementation until replacement acceptance passes. Quest Convenience is the following separate migration.
