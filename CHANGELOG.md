# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/realistic-orbital-trade/releases/tag/v0.1.0
