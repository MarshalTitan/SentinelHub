# Sentinel Hub 0.8.0.0

Adds optional Treasure Maps with deciphered-map location detection, a Party map queue (Alliance opt-in), exact raw flags and map variants, Open/Next, Complete with brief Undo, Delete/reorder, an independent floating window and local character-scoped unfinished queues. Existing settings and Core/UI 0.4.1 are preserved; Treasure Maps defaults Off.

Share restores the saved game flag and copies /p <flag> for manual sending of a real clickable link. It does not post chat automatically, teleport or navigate. No external server or other plugin is required.

Processing is event-driven with dormant idle guards, cached metadata and bounded framework-thread work. Existing performance fixes remain intact. See docs/TREASURE_MAPS.md for API/license review, limitations, measurements and supervised checks. Native detection, actual map/chat interaction and gameplay performance are not certified by automated tests. This release is distributed through the normal live plugin channel; the optional module remains Off until enabled.
