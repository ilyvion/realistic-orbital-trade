# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

-   Apparently, the order of elements in LoadFolders.xml matters, but RimWorld doesn't think to mention this to you until you try to update your mod on the Steam Workshop. 🙃 I guess we're skipping 0.3.0 on Steam, heh.

## [0.3.0] - 2024-05-09

### Added

-   If [We Had a Trader?](https://steamcommunity.com/sharedfiles/filedetails/?id=1541408076) or [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) are active, Realistic Orbital Trade patches their alerts about orbital trade ships to include information about when they close their comms if the player triggered extra grace time in a trade.
-   If [Tabula Resa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) is active, make use of its "update log" feature.

### Fixed

-   There was a bug in the code that loaded trade information associated with trade ships, which meant that if you loaded a game while there was an orbital trader active with a grace time set, "time until comms close" got reset, and you could once again make trades with the trader even during the grace period. Similarly, you could initiate a second trade with a trader while another trade was already ongoing under the same conditions.

## [0.2.0] - 2024-05-04

### Added

-   Support for RimWorld 1.4

## [0.1.1] - 2024-05-01

### Changed

-   Assume that if a trade is taking place but a Realistic Orbital Trade agreement isn't in place, that a different mod is in effect and ignore it. Fixes a discovered incompatibility with the Trade Ships mod.

### Fixed

-   Typo in Preview image

## [0.1.0] - 2024-04-30

### Added

-   First implementation of the mod.

[Unreleased]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/realistic-orbital-trade/releases/tag/v0.1.0
