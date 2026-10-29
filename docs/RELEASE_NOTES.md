# Sentinel Hub 0.8.1.0

Both Treasure Maps queue views now provide every per-entry action: Open Flag, Share, Complete, visible Up/Down arrows and More with Delete, Move Up, Move Down and Add another. The floating window includes the full scrollable queue. Compact controls wrap to fit narrow windows.

More uses native auto-sizing menu items in an independent popup, fixing the clipped menu and allowing ImGui to reposition it inside the game viewport. Boundary reorder controls are disabled. Order is shared immediately and saved by the existing character-scoped queue; Next Map follows its first entry. Complete still removes a row, and Undo restores its prior position when possible.

Existing queues, settings, geometry, floating X/Show/Hide behavior, decipher detection, structured chat capture, manual flag sharing and event-driven performance scheduling are preserved. No native detection, dependency or background processing changes. Normal live plugin updates deliver this release.
