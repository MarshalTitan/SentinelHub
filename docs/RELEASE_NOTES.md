# Sentinel Hub 0.8.0.2

Fixes the floating Treasure Map Queue's title-bar X: it now closes the window and saves the closed preference before the next frame can reopen it. Hub changes back to Show Floating Queue, and Show/Hide can continue toggling the same window normally.

Queue contents and saved geometry are preserved. Close notifications save only once. Native ImGui regression coverage exercises X close, the deferred close callback and the actual Show/Hide buttons in both themes at three UI scales.

The 0.8.0.1 decipher-detection fix and previous event-driven performance improvements are retained. Normal live plugin updates deliver this change.
