# Sentinel Hub 0.8.0.1

Fixes automatic treasure-map detection after deciphering or reopening a map. The initial detector matched an unrelated UI helper; the corrected binding identifies the treasure call by its rank/spot/mode arguments and separately validates the target handler before installing the observer.

The binding was checked offline against game build 2026.10.16.0000.0000. Regression tests reject the former wrong call, malformed/out-of-range targets and incompatible argument reads. Existing party queues, saved settings, manual flag sharing and previous performance fixes are preserved. There is no added polling or background scanning.

Normal live plugin updates deliver this fix. An already-deciphered map can be viewed again to trigger detection; it does not need to be discarded. In-game map opening, location accuracy and movement behavior remain separate from offline/automated validation.
