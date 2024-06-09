# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.7.0] - 2024-06-09

### Added

-   Added a mechanism to renegotiate a trade after it is already started. This should help people who get stuck being unable to complete a trade because of an item no longer existing or having been damaged too much.

### Changed

-   Switched to using reverse patching for the custom transport ship inspect tab. This avoids other mods stepping on our toes and us stepping on other mods' toes.

### Fixed

-   Made it so the shuttle loading code accepts a small difference in `Thing`'s hitpoint value so that a small amount of deterioration doesn't stop a trade from happening.

## [0.6.0] - 2024-05-18

### Added

-   Added a compatibility layer for [Auto Seller](https://steamcommunity.com/sharedfiles/filedetails/?id=1440321094)'s AI trading. This may be a bit fragile due to how I had to do it by patching private methods in that mod, but at least it works at the time this version is published.

## [0.5.0] - 2024-05-15

### Added

-   Added a compatibility layer for [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506). This is required because Dynamic Trade Interface uses a custom trade dialog instead of modifying the original, so Realistic Orbital Trade wasn't notified when a trade was cancelled when using it, leading to Realistic Orbital Trade thinking a trader had an active trade agreement when there wasn't any. This has now been corrected.

### Fixed

-   The fix for minified items from last version had a bug where it would show items with multiple counts as "ItemName x&lt;stackCountInStorage&gt; x&lt;stackCountNeeded&gt;" in the shuttle's "Content" tab. This has now been fixed to be just "ItemName x&lt;stackCountNeeded&gt;" as it should be.

## [0.4.0] - 2024-05-12

### Added

-   Added an option to remove the time pressure aspect of the mod. Normally (i.e. before this release or in future releases with this new option unset), the orbital trader's depature timer will keep counting down even while there's an active trade. This adds an element of time pressure to the mod, which was how it was intended to be experienced. However, because different people have different playstyles and preferences, I've decided to add this option so that when it is set, an active trade stops the orbital trade ship from counting down its departure, and you can take as much time as you need to finish your trades.

### Changed

-   The way grace time was calculated was not very intuitive. Instead of adding the grace time to the existing time, the time was directly set to the grace time value. This meant that you could end up in a situation where when you were supposed to have grace time added, it would actually give you _less_ time instead of more! For instance if the trader was leaving in 6 hours and you had set your grace time setting to 4 hours, the new trader leave time would be in 4 hours, not the expected 10! The logic is now that the grace time is added on top of the existing time, not replacing it.
-   Trade ships will now leave an hour after the last trade if their comms were closed; there's no reason to keep them around any longer than that.
-   When you get blacklisted, all active trade quests will be cancelled, not just the one whose shuttle you destroyed.
-   Supress the "Shuttle is loaded with required cargo and can launch when ready" message that would appear after a trade shuttle was fully loaded; an artefact of vanilla-inherited behavior.

### Fixed

-   If you cancelled a shuttle immediately after it landed, before it had time to build up its list of items to load, it would produce a `NullReferenceException`. This no longer happens.
-   Minified items weren't being handled correctly, so any minified item would count as matching any other. This has now been corrected so that the "inner" item of the minified item is used for checking whether a given item matches what was traded for.

## [0.3.1] - 2024-05-09

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

[Unreleased]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.7.0...HEAD
[0.7.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/realistic-orbital-trade/releases/tag/v0.1.0
