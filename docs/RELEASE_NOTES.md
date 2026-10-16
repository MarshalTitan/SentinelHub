Sentinel Hub 0.3.1.1 corrects plugin inventory when live and dev copies share an internal name.

- Dashboard and Plugins prefer the installed non-dev copy, independent of enumeration order.
- Version and state come from the same selected installation, using Dalamud's effective version.
- Open settings requires that selected live copy to be loaded and unrestricted.
- Development-only installations remain visible and are labeled.
- No hardcoded SRank version, consumer/gameplay/configuration or utility changes.

Cleanup PASS recorded. [Focused acceptance](LIVE_DEV_DISCOVERY.md): expect your live SRank 0.7.56.0, rather than its dev version.
