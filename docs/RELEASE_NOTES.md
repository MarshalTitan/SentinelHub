Sentinel Hub 0.4.0.0 adds default-OFF Latency / Weaving proving.

- Off / Measure only / Compensate modes, with bounded own instant-action response correction.
- Fixed 40 ms response buffer, 250 ms maximum correction and 500 ms correlation timeout.
- Public loaded NoClippy and native XivAlexander module/export conflict checks; no override.
- Typed action-request/effect hooks only while eligible, exact-once original forwarding and scoped cleanup.
- Cast-time/long/special locks, packets, cooldowns and action selection remain untouched.
- In-memory response/count diagnostics; export contains only allowlisted state and bounded numbers.
- Preserves Anti-AFK/Quest engines/settings, live/dev inventory fix, HUD and Core/UI pins.

Native in-game acceptance remains pending. See [procedure, source audit and rollback](LATENCY_WEAVING.md).
