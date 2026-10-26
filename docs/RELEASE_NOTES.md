# Sentinel Hub 0.7.1.0

Reduces avoidable idle work with shared event-driven plugin inventory, cached Health/Compatibility snapshots, snapshot-triggered Activity, dormant closed collections/Inspector and addon-driven Quest scheduling with a bounded fallback. Existing Quest native timing/safety and Anti-AFK timer behavior remain unchanged.

Unifies latency conflict protection. No full native scan while latency is Off or from native action callbacks; active safety scans use a bounded reusable handle buffer. Pre-enable checks, named-module checks, loaded NoClippy protection and conservative failures remain in place.

Replaces the Profiles UNKNOWN placeholder with saved profiles, selected/last-applied and authoritative match/drift/busy status from Sentinel Profiles 0.2.1.8. Open in Profiles selects/opens the existing editor without applying or changing plugin rules. Missing/disabled/older providers get concise explanations.

Adds explicit RAM-only /shub perf start, report and stop measurement (ten-minute limit). Normal release operation does not sample or write performance history. Saved settings/layout, collections/acquisition data and Core 0.4.1 stay intact.

See docs/PERFORMANCE_PROFILES.md for measurements, residual fallback checks and supervised acceptance. The user authorized normal live publication on 2026-10-26 so their girlfriend can evaluate the differences. Pre-release movement/interaction acceptance is deferred; a resolved stutter or new in-game PASS is not claimed.
