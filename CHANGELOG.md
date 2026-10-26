# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Dependencies

- ilyvion's Laboratory: v0.27.0

> [!IMPORTANT]  
> This release requires an update to ilyvion's Laboratory!

## [0.15.0] - 2026-10-25

### Added

- A purchase no longer has to fit on a single trade shuttle. If what you're buying is heavier than one shuttle can carry, the trader now delivers it with as many shuttles as it takes, one after another: each lands, unloads its share and leaves before the next one arrives, and the trade finishes once everything has been delivered. This also applies when renegotiating a trade to buy more. Any single item or creature you buy still has to fit on one shuttle.
- Compatibility with Find / Trade's automatic bartering. Trades it makes with orbital traders now go through trade shuttles like any other trade, and goods already sold to one trader aren't sold again to another, also when Find / Trade fills in a trade window you open yourself. Items or creatures too heavy for a shuttle are left out of these trades, and a trade worth less than the trader's minimum trade value is skipped. Renegotiating a trade on a landed shuttle opens with just the agreed deal, without Find / Trade adding your junk or other items to it.

## [0.14.2] - 2026-10-22

### Fixed

- Fixed loading a save logging errors about `pendingMassShortfall` when it was saved while a colonist was hauling goods to a trade shuttle.

## [0.14.1] - 2026-10-17

### Fixed

- Fixed loading a save failing with an error when it was saved after a trade shuttle had flown off but before the trade had finished, such as while waiting for the payment shuttle or just after it left.

## [0.14.0] - 2026-10-17

### Dependencies

- ilyvion's Laboratory: v0.25.1

> [!IMPORTANT]  
> This release requires an update to ilyvion's Laboratory!

### Added

- Support for RimWorld 1.7.

### Fixed

- Fixed an error with Social Experience from Trade installed when accepting a renegotiated trade, or when the trade dialog refused a deal (for example, one below the minimum trade value or too heavy for the shuttles).
- Fixed renegotiating a trade with Social Experience from Trade installed not changing the social experience the negotiator gets when the trade completes, or adding the whole renegotiated deal on top of the original one. The experience now matches the renegotiated deal.
- Fixed an error when colonists loaded items into a trade shuttle after another mod had closed the trade session.

## [0.13.1] - 2026-10-11

### Fixed

- Fixed being able to sell the same items to two traders at once. Items and creatures you've already sold in an ongoing trade, including silver you've agreed to pay, are no longer offered when trading with another trader, whether in orbit or visiting. If only part of a stack was sold, only the rest of it is offered.
- Fixed renegotiating a trade while Bulk Load For Transporters was loading the shuttle not offering the goods its loaders were carrying.
- Fixed items left over after Bulk Load For Transporters loaded a trade shuttle staying protected from colony jobs until the trade ended, and causing a warning in the log when the shuttle took off.
- Fixed sold items that a colonist dropped on the way to the trade shuttle, for example when interrupted or downed, being free for other colonists to eat, wear or use before they were carried to the shuttle again. They now stay set aside for the trade.
- Fixed renegotiating a trade with GTG Trader Core installed mixing up damaged items of the same kind, such as several flak vests with slightly different condition. Renegotiating could take sold items off the deal or try to sell more than the colony had, and reported an error, even when nothing was changed in the dialog.

## [0.13.0] - 2026-09-26

### Added

- A sale no longer has to fit on a single trade shuttle. If what you're selling is heavier than one shuttle can carry, the trader now sends as many shuttles as it takes, all at once. Colonists load them all, and they take off together once every one of them is fully loaded. Cancelling the trade, running out of time, or renegotiating applies to all the shuttles together. Any single thing you buy still has to fit on one shuttle, so a single item or creature too heavy for one shuttle still can't be sold.

### Fixed

- Fixed animals you bought never arriving when the wait for delivery, by shuttle or drop pods, was long enough for the game to clean up creatures nothing was keeping track of. They were discarded while waiting, but were still removed from the trader's stock. Trades already in progress from before this fix are not protected.
- Fixed renegotiating a trade for more silver than the trader has left reporting an error. You now receive as much as the trader can pay, just like when the trade was first made.
- Fixed colonists sometimes loading a few more items than were sold when two of them were loading at the same time, most often after increasing a sale. The extra items were lost to the trader without payment.
- Fixed items left over after reducing a sale, including when the trader offers to renegotiate down to what was already loaded, staying protected from colony jobs and causing a warning in the log when the shuttle took off.

## [0.12.2] - 2026-09-20

### Added

- A trade shuttle now tells you when sold items are lying outside the home area and outside any storage, which is the one place colonists never look when loading. It shows a new warning icon and an inspect message explaining that you can expand the home area over the items, move them into storage, or renegotiate.

## [0.12.1] - 2026-09-18

### Changed

- When an item, colonist or other creature already sold to a trader is lost before delivery, the letter now names the specific thing that was lost. The trade shuttle's inspect string and its Contents tab now also list exactly what's missing from the deal, instead of just saying something was.
- Reopening a trade to renegotiate after losing sold items now immediately explains what was lost and how much the silver you owe changed to compensate, instead of leaving you to notice the adjustment on your own.
- Losing a sold item that's still available elsewhere in the colony (e.g. one packaged meal out of a larger batch) no longer forces a renegotiation. The shuttle now substitutes an equivalent item from your remaining stock instead, so renegotiation is only required when the loss can't be made up automatically.

### Fixed

- Fixed a crash on the first load of a save after enabling the Social Experience from Trade integration, which happened whenever that save had no orbital trade data saved for it yet.

## [0.12.0] - 2026-09-04

### Added

- Added an integration with Social Experience from Trade: the social experience it grants for a trade is now held back until the trade ship's delivery actually finishes, instead of being granted the instant a deal is accepted. Without this, a trade could be accepted and immediately cancelled to farm experience without ever completing a real delivery.

### Fixed

- Fixed a vanilla issue where, when a sale included several animals of the same kind (e.g. two Huskies), the shuttle's 'To load' list dropped the entry entirely as soon as one of them was picked up and being led over, instead of showing the remaining count until all of them were delivered.

## [0.11.4] - 2026-08-29

### Changed

- The warning icon shown when an item is missing was changed to make it more visually specific.
- A trade shuttle now shows a distinct warning icon when a required item is present but forbidden, instead of sharing the same icon used for a missing item.

### Fixed

- Selling several identical items of the same type, material and quality only showed and hauled one of them at a time, forcing a separate hauling trip for each instead of loading them all together in one trip.

## [0.11.3] - 2026-08-27

### Fixed

- When a trade required several specific items that shared a base item type but differed in material or quality (e.g. several different tuques), the shuttle's 'To load' list and required-cargo summary would only ever show one of them at a time instead of all of them together, forcing a separate hauling trip for each one in turn. They're now all listed and haulable together, so multiple different variants of the same item can be picked up and carried in a single trip.
- A trade shuttle now shows the same pulsing warning icon and inspect string as a genuinely missing required item when a required item is present in the colony but too much of it is forbidden for colonists to actually haul it.
- The 'finished loading' message now correctly stays hidden for trade shuttles when the last item is loaded.
- You no longer get a spurious 'sold trade goods lost' letter and renegotiation prompt when an item you sold gets destroyed after hauling had already picked up an equivalent amount of the same item from elsewhere to load onto the shuttle instead - the sale was already fully covered, so its loss no longer counts against the deal.

## [0.11.2] - 2026-08-11

### Changed

- The 'Multi-item shuttle loading' setting now shows a warning when Bulk Load For Transporters is installed and the setting is turned off, since that mod takes over shuttle loading in that case but doesn't know how to haul genepacks or xenogerms out of genebanks.

### Fixed

- Colonists could still stand around doing nothing instead of loading a trade shuttle when a mod like Bulk Load For Transporters was installed, even after the previous fix for this: that fix only covered the moment such a mod builds its own hauling job, not the mod's own job execution afterward, which can recheck whether a pawn may pick up each item well into the trip. That recheck kept seeing already-sold items as off-limits, so a colonist would jump straight to giving up on the job (without ever actually walking anywhere) the instant it started, over and over. The allowance now stays in effect for as long as a pawn's current job is hauling to that shuttle, not just while the job is being built.

## [0.11.1] - 2026-08-11

### Fixed

- Fixed colonists getting stuck instead of loading a trade shuttle when another mod like Bulk Load For Transporters was installed: standing still doing nothing, never picking up any hauling job at all, or endlessly reissuing a short expiring Wait job. These mods' own transporter-loading or job-building logic re-checks whether a pawn may pick up each item, and that recheck ran outside the same allowance this mod's job-search step already granted it, so it kept seeing already-sold items as off-limits to the very pawn entitled to haul them. With multi-item shuttle loading turned on, this mod is now the sole authority for loading trade shuttles, so other mods' own transporter-loading logic no longer gets a chance to interfere with them.
- With multi-item shuttle loading turned on, a hauling trip could occasionally leave a single unit of an item behind on the map (needing an entire separate trip to fetch later) instead of carrying it along with the rest of the stack it came from, when the amount that actually fit in the hauler's inventory came in just under what was planned for.
- The missing-cargo warning (pulsing shuttle icon and inspect string) never triggered for a shortfall in a plain required item like wood, steel, or meals - only for genepacks and quality-tracked apparel. If a required stack got picked up into a colonist's inventory instead of hauled to the shuttle, the trade could get stuck waiting on cargo that would never show up, with no indication anything was wrong.

## [0.11.0] - 2026-08-11

### Added

- You can now right-click a trade shuttle with another colonist and choose 'Take over as trade negotiator' if the original negotiator has died or otherwise become unavailable while the trade is still ongoing. This lets autoloading and renegotiation resume normally instead of staying stuck. You'll receive a letter as soon as an active trade's negotiator becomes unavailable, so you don't have to discover it yourself.d
- A trade shuttle that needs renegotiating now shows a pulsing icon over it, so you can spot at a glance when a trading shuttle needs your attention.
- Genepacks stored in a genebank now get hauled to the shuttle. This also applies to modded genebanks, as long as they use the vanilla genepack-container building logic. Fixes [#10](https://github.com/ilyvion/realistic-orbital-trade/issues/10).
- Genepacks and xenogerms required by a trade shuttle now show up as their own separate entries in its Contents tab's 'To load' list and in its required-cargo summary, labeled by their specific gene set (e.g. 'Genepack (psy-deaf)') instead of being lumped together under a single generic 'Genepack' label.
- You can now right-click a trade shuttle and choose 'Buy more time' to pay silver to extend its departure timer, in case a large trade needs longer to load than the shuttle would normally wait around for. Each purchase on the same shuttle costs more silver than the last, so it can't be used to stall indefinitely without becoming not worth it. The time added per purchase, the base silver cost, and how quickly the cost grows are all configurable in the mod settings. Resolves [#3](https://github.com/ilyvion/realistic-orbital-trade/issues/3).
- The trade dialog (including the Dynamic Trade Interface integration) now shows the combined mass of everything you're selling to the trader, and everything you're buying from them (including the silver changing hands), against the trade shuttle's cargo capacity, turning red once you go over. The incoming row only appears when bought items are set to arrive by shuttle rather than drop pod. The vanilla trade dialog's item table now shows each item's mass, turning red once adding one more would exceed whichever shuttle it's headed for, and gained 'M<'/'>M' buttons (alongside the existing '<'/'>>') to add or remove as much of an item as fits within that shuttle's remaining cargo capacity, mirroring the mass column and buttons from the Form Caravan dialog. In the Dynamic Trade Interface integration, its own count controls (the number field and the button-based +/- column) now stop at that same mass limit instead. Trades whose outgoing or incoming mass would still exceed the shuttle's capacity are rejected by the trader when you click Accept, instead of letting you strike a deal the shuttle can never fully load; the incoming check is skipped when bought items are set to arrive by drop pod instead, since those aren't mass-limited. Fixes [#11](https://github.com/ilyvion/realistic-orbital-trade/issues/11).
- Added a new 'Multi-item shuttle loading' setting that lets colonists carry several stacks of goods at once when loading a trade shuttle, instead of hauling one stack per trip, which speeds up loading large trades considerably. It's automatically turned on by default, but you can enable or disable it at any time. Turning it off returns vanilla's shuttle-loading code, which loads a single stack of items at a time. If a shuttle's load is cancelled while a colonist is mid-trip (e.g. the trader leaving early), any extra goods they were carrying for that trip are dropped on the spot instead of staying in their inventory.
- Added an 'Item condition matching tolerance' setting. When a traded item's condition affects its price, the shuttle requires the hauled item to still be in essentially the same condition it was in when the deal was struck; this setting lets its hit points drift by up to a percentage of its max (or 5 hit points, whichever is larger) and still count as a match, so a small amount of deterioration (e.g. from fire or vermin) while the item is waiting to be hauled doesn't get a trade stuck. Defaults to 2%.
- Items sold to a trader are now protected from other colony jobs the moment the deal is struck, so colonists no longer automatically eat, wear, or craft with something that's already been sold before a hauler gets around to loading it. Only sold items on the map are protected; items already sold under a trade that was in progress before this update won't retroactively gain protection. Attempting to manually ordering a pawn (e.g. via right-click 'Pick up') to take, wear, equip, or otherwise use something already sold to a trader is now labeled 'reserved for trade' and disabled, and if a job still slips through some other way, it now shows a message explaining the item is reserved instead.
- A trade shuttle whose required cargo can't currently be found anywhere in the colony (consumed, worn, hidden away, etc.) now shows a pulsing icon over it and a note in its inspect string, so it's no longer a mystery why loading has stalled. Picking an item up to haul it doesn't trigger this.
- If a trader leaves before its shuttle finished loading, you're no longer forced to lose everything: whatever's already loaded is locked in and the trade dialog reopens automatically so you can adjust what you're buying to match, instead of the shuttle immediately dumping its cargo and the trade failing outright. You can't change what's already been loaded at this point, only reduce what you're buying to balance the deal. Closing the dialog without accepting still fails the trade like before, but now asks you to confirm first so it can't happen by accident. Resolves [#7](https://github.com/ilyvion/realistic-orbital-trade/issues/7).

### Changed

- The trade shuttle is now labeled 'trade shuttle' instead of 'imperial shuttle', with a description explaining its role in orbital trade. The explosive-rigged shuttles sent when a trader no longer trusts you (whether for a regular delivery or while you're trying to make amends) now have their own descriptions explaining why they've shown up wired to blow.
- Renegotiating a trade shuttle's cargo now reopens the regular trade window (or the Dynamic Trade Interface window, if you have that mod installed), pre-loaded with everything already agreed to, instead of a separate, more limited renegotiation dialog. This means you can now add new items to the deal during renegotiation, not just remove or reduce what's already agreed - useful if you want to sell more to help fill out the shuttle, or realize you want to buy something you skipped the first time around. The shuttle's cargo mass capacity is enforced the same way it is for a fresh trade.

### Fixed

- Fixed a pawn sold to a trader, or bought from one and later handed back (trade cancelled, shuttle destroyed or expired, or the player got blacklisted), sometimes ending up registered both as a world pawn and inside the trader's cargo, which caused a 'load ID already used' error and a corrupted duplicate of the pawn when reloading the save.
- Added a load-after entry for The Dead Man's Switch so this mod loads correctly alongside it. Fixes [#5](https://github.com/ilyvion/realistic-orbital-trade/issues/5).
- Fixed numerous bugs in trade renegotiation, most of them in the old, separate renegotiation dialog now replaced by reopening the trade window (see above): reducing a required item that had quality, health, or 'minified' requirements didn't actually reduce the amount owed; renegotiated tradeables could be paired up by item def instead of by the exact item, which could crash on Accept and could drop another required item from the shuttle's 'Required' summary entirely instead of just reducing it; shuttle requirement labels could be missing an item's material name or health percentage; a cancelled shuttle load could un-cancel itself after a save/reload; destroying an item already sold to the trader could crash the game, both immediately and after a save/reload, instead of removing it from the deal, adjusting the shuttle's required cargo, and prompting you to renegotiate; a shuttle whose trade component had gone missing could crash the game when you tried to renegotiate or register as its negotiator (Fixes [#8](https://github.com/ilyvion/realistic-orbital-trade/issues/8)); the amount of an item available to sell could be miscalculated once some of it was already loaded onto the shuttle or being carried there by a hauling colonist; animals bought or sold weren't properly transferred; and unselling a single genepack could eject or misload every genepack already on the shuttle instead of just that one.
- A trade shuttle's 'Required' list now shows only what's still left to load, instead of always listing the full original amount of an item until it's been fully loaded.
- The mod settings page now scrolls instead of clipping the bottom controls when the window isn't tall enough to show every setting at once (smaller resolutions, higher UI scale, etc.).

## [0.10.2] - 2025-09-12

### Fixed

- Add VersionCheck def for ilyvion's Laboratory.

## [0.10.1] - 2025-09-11

### Fixed

- Add dependency for ilyvion's Laboratory in About.xml.

## [0.10.0] - 2025-09-10

### Added

- Added a setting for receiving trade goods by drop pod instead of shuttle for that slightly more vanilla feel.

### Fixed

- Scribe value of 'trade pauses departure timer' in TradeAgreement.

## [0.9.0] - 2025-07-04

### Added

- Added a dialog to get people to add a future dependency. Since Rimworld doesn't have a good mechanism to automatically add new dependencies, we need to be a little in-your-face about it.
- Rimworld 1.6 support.

### Changed

- Attempted to make the transpiler for the Dynamic Trade Interface integration more robust

## [0.8.1] - 2024-07-06

### Changed

- Made "parent" `ThingDef`s `Abstract` in the hope that it'll get a weird mod interaction to go away.

## [0.8.0] - 2024-06-21

### Added

- Added a mechanism for traders to have a minimum trade deal value requirement. It is off by default, but turning it on makes it so trader won't accept trades that involve goods of a total value lower than the set number. This is meant to add to the realism a bit to avoid traders sending whole transport ships to deliver a single bottle of beer.
- Patched transport hauling code so that hauling books out of bookcases now works as expected.

### Changed

- The second shuttle that delivers goods to the player now chooses its landing spot just-in-time instead of ahead-of-time. This should hopefully avoid things like it landing on top of other shuttles that have arrived in the meantime since the landing spot was originally chosen.

## [0.7.1] - 2024-06-09

### Fixed

- Fixed a bug in the way renegotation calculations were done.

## [0.7.0] - 2024-06-09

### Added

- Added a mechanism to renegotiate a trade after it is already started. This should help people who get stuck being unable to complete a trade because of an item no longer existing or having been damaged too much.

### Changed

- Switched to using reverse patching for the custom transport ship inspect tab. This avoids other mods stepping on our toes and us stepping on other mods' toes.

### Fixed

- Made it so the shuttle loading code accepts a small difference in `Thing`'s hitpoint value so that a small amount of deterioration doesn't stop a trade from happening.

## [0.6.0] - 2024-05-18

### Added

- Added a compatibility layer for [Auto Seller](https://steamcommunity.com/sharedfiles/filedetails/?id=1440321094)'s AI trading. This may be a bit fragile due to how I had to do it by patching private methods in that mod, but at least it works at the time this version is published.

## [0.5.0] - 2024-05-15

### Added

- Added a compatibility layer for [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506). This is required because Dynamic Trade Interface uses a custom trade dialog instead of modifying the original, so Realistic Orbital Trade wasn't notified when a trade was cancelled when using it, leading to Realistic Orbital Trade thinking a trader had an active trade agreement when there wasn't any. This has now been corrected.

### Fixed

- The fix for minified items from last version had a bug where it would show items with multiple counts as "ItemName x&lt;stackCountInStorage&gt; x&lt;stackCountNeeded&gt;" in the shuttle's "Content" tab. This has now been fixed to be just "ItemName x&lt;stackCountNeeded&gt;" as it should be.

## [0.4.0] - 2024-05-12

### Added

- Added an option to remove the time pressure aspect of the mod. Normally (i.e. before this release or in future releases with this new option unset), the orbital trader's depature timer will keep counting down even while there's an active trade. This adds an element of time pressure to the mod, which was how it was intended to be experienced. However, because different people have different playstyles and preferences, I've decided to add this option so that when it is set, an active trade stops the orbital trade ship from counting down its departure, and you can take as much time as you need to finish your trades.

### Changed

- The way grace time was calculated was not very intuitive. Instead of adding the grace time to the existing time, the time was directly set to the grace time value. This meant that you could end up in a situation where when you were supposed to have grace time added, it would actually give you _less_ time instead of more! For instance if the trader was leaving in 6 hours and you had set your grace time setting to 4 hours, the new trader leave time would be in 4 hours, not the expected 10! The logic is now that the grace time is added on top of the existing time, not replacing it.
- Trade ships will now leave an hour after the last trade if their comms were closed; there's no reason to keep them around any longer than that.
- When you get blacklisted, all active trade quests will be cancelled, not just the one whose shuttle you destroyed.
- Supress the "Shuttle is loaded with required cargo and can launch when ready" message that would appear after a trade shuttle was fully loaded; an artefact of vanilla-inherited behavior.

### Fixed

- If you cancelled a shuttle immediately after it landed, before it had time to build up its list of items to load, it would produce a `NullReferenceException`. This no longer happens.
- Minified items weren't being handled correctly, so any minified item would count as matching any other. This has now been corrected so that the "inner" item of the minified item is used for checking whether a given item matches what was traded for.

## [0.3.1] - 2024-05-09

### Fixed

- Apparently, the order of elements in LoadFolders.xml matters, but RimWorld doesn't think to mention this to you until you try to update your mod on the Steam Workshop. 🙃 I guess we're skipping 0.3.0 on Steam, heh.

## [0.3.0] - 2024-05-09

### Added

- If [We Had a Trader?](https://steamcommunity.com/sharedfiles/filedetails/?id=1541408076) or [Tweaks Galore](https://steamcommunity.com/sharedfiles/filedetails/?id=2695164414) are active, Realistic Orbital Trade patches their alerts about orbital trade ships to include information about when they close their comms if the player triggered extra grace time in a trade.
- If [Tabula Resa](https://steamcommunity.com/sharedfiles/filedetails/?id=1660622094) is active, make use of its "update log" feature.

### Fixed

- There was a bug in the code that loaded trade information associated with trade ships, which meant that if you loaded a game while there was an orbital trader active with a grace time set, "time until comms close" got reset, and you could once again make trades with the trader even during the grace period. Similarly, you could initiate a second trade with a trader while another trade was already ongoing under the same conditions.

## [0.2.0] - 2024-05-04

### Added

- Support for RimWorld 1.4

## [0.1.1] - 2024-05-01

### Changed

- Assume that if a trade is taking place but a Realistic Orbital Trade agreement isn't in place, that a different mod is in effect and ignore it. Fixes a discovered incompatibility with the Trade Ships mod.

### Fixed

- Typo in Preview image

## [0.1.0] - 2024-04-30

### Added

- First implementation of the mod.

[Unreleased]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.15.0...HEAD
[0.15.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.14.2..v0.15.0
[0.14.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.14.1..v0.14.2
[0.14.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.14.0..v0.14.1
[0.14.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.13.1..v0.14.0
[0.13.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.13.0..v0.13.1
[0.13.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.12.2..v0.13.0
[0.12.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.12.1..v0.12.2
[0.12.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.12.0..v0.12.1
[0.12.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.11.4..v0.12.0
[0.11.4]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.11.3..v0.11.4
[0.11.3]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.11.2..v0.11.3
[0.11.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.11.1..v0.11.2
[0.11.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.11.0..v0.11.1
[0.11.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.10.2..v0.11.0
[0.10.2]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.10.1..v0.10.2
[0.10.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.10.0..v0.10.1
[0.10.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.9.0..v0.10.0
[0.9.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.8.1...v0.9.0
[0.8.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.8.0...v0.8.1
[0.8.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.7.1...v0.8.0
[0.7.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.7.0...v0.7.1
[0.7.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/ilyvion/realistic-orbital-trade/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/ilyvion/realistic-orbital-trade/releases/tag/v0.1.0
