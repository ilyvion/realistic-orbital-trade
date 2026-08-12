[![RimWorld 1.4](https://img.shields.io/badge/RimWorld-1.4-brightgreen.svg)](http://rimworldgame.com/) [![RimWorld 1.5](https://img.shields.io/badge/RimWorld-1.5-brightgreen.svg)](http://rimworldgame.com/) [![RimWorld 1.6](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](http://rimworldgame.com/) [![Build](https://github.com/ilyvion/realistic-orbital-trade/actions/workflows/build.yml/badge.svg)](https://github.com/ilyvion/realistic-orbital-trade/actions/workflows/build.yml)

Has it ever bothered you how the orbital traders can just magically teleport items right out of your stockpiles? This mod puts an end to that. With **Realistic Orbital Trade**, your pawns will now have to load up a shuttle with the goods you sold before the orbital trader gets their hands on them AND before you get your hands on the goods you bought.

The mod will guide you through all the required steps using the RimWorld quest and letter systems once you've made a trade with an orbital trader.

The mod can be added to a game at any time. Removing the mod should be fine as long as there is no active trading quest at the time. There will be a fairly large list of once-off errors when first loading such a save, however.

## Requirements

This mod requires:

- **RimWorld: Royalty** (the DLC that introduced shuttles, which this mod builds on)
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [ilyvion's Laboratory](https://steamcommunity.com/sharedfiles/filedetails/?id=3296362231), a shared utility mod

## Features

A few highlights beyond the core shuttle-loading behavior:

- Renegotiate a trade (right-click the trade shuttle with a pawn) at any time, even after a trade agreement has already been reached. Also useful if an item you sold becomes unavailable or too damaged to be sold anymore.
- With the 'multi-item shuttle loading' feature, colonists carry several stacks of goods per trip instead of one, speeding up large trades considerably.
- Pay silver to 'buy more time' on a shuttle's departure timer if a big trade needs longer to load than it would normally wait around for.
- The trade window shows and enforces the shuttle's cargo mass capacity, so you can't strike a deal it can't carry.
- Genepacks and xenogerms stored in genebanks (vanilla or modded) get hauled out to the shuttle automatically.
- If your negotiator dies or otherwise becomes unavailable mid-trade, another colonist can take over so the trade doesn't get stuck.
- Optional settings for a minimum trade value, delivering bought items by drop pod instead of shuttle, and how much item wear-and-tear is tolerated.

## Mod compatibility

Mod compatibility is probably generally good, but there might be issues with mods that modify how orbital traders work or how vanilla shuttles work.

### Explicitly supported

- [Dynamic Trade Interface](https://steamcommunity.com/sharedfiles/filedetails/?id=3020706506) — A compatibility layer has been added to make DTI's custom trade window work correctly with ROT.
- [Auto Seller](https://steamcommunity.com/sharedfiles/filedetails/?id=1440321094) — A compatibility layer has been added to make Auto Seller's AI trader work correctly with ROT.
- [Bulk Load For Transporters](https://steamcommunity.com/sharedfiles/filedetails/?id=3559292908) — With ROT's 'Multi-item shuttle loading' setting on (the default), ROT is the sole authority for loading trade shuttles, so Bulk Load For Transporters' own loading logic doesn't interfere with it. Turning that setting off lets Bulk Load For Transporters take over shuttle loading instead, though it currently can't haul genepacks or xenogerms out of genebanks, so those get left behind in that case.

### Works with tweaks

- [Call a trader](https://steamcommunity.com/sharedfiles/filedetails/?id=2784149999) — Reported to work by another player if the 'Absolute Cost Range' values (both numbers) in the mod settings are set to zero.

## Troubleshooting

### My colonists won't load certain items onto the shuttle

First, make sure the items you're trying to sell are in the colony's home area. The code used by shuttles to find sendable items uses the same logic as the code for forming a caravan does for finding valid items, which has the same requirement. This is a vanilla behavior that I'm not particularly interested in modifying.

Items sold to a trader are protected from other colony jobs the moment the deal is struck, so colonists shouldn't automatically eat, wear, or craft with something that's already been sold before a hauler gets around to loading it. That protection doesn't apply retroactively to a trade that was already in progress when you update the mod, though, and it can't stop an item from disappearing some other way (destroyed, stolen in a raid, etc.). If a required item is no longer available, right-click the shuttle to renegotiate the trade and remove it from the deal instead of failing the trade.

Finally, make sure items are stored safely so they don't deteriorate too much. If you sold an item at 50% and it's now at 48%, that's not quite the item you promised to the trader anymore, and the shuttle won't accept it. The 'item condition matching tolerance' setting (2% by default) allows a small amount of wear and tear before this becomes a problem.

### Bug reports

There's an ongoing issue where people report that this mod causes problems with other shuttles. Unfortunately, every time I test these scenarios myself, everything works fine. As such, I suspect there is another mod that is causing problems, but so far I have nothing solid to go on. Should you experience this kind of problem, help me help you by doing the following:

1. Make sure you have HugsLib or [The Log Publisher from HugsLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2873415404).
2. Do whatever causes the error to show up.
3. Press Ctrl + F12, click Upload, wait until it's done, and then click Copy.
4. Paste the link into a new [GitHub issue](https://github.com/ilyvion/realistic-orbital-trade/issues), along with any additional information you have, if any.

## Translations

- [Chinese](https://steamcommunity.com/sharedfiles/filedetails/?id=3238838766) by [多事之\_](https://steamcommunity.com/profiles/76561198986560305)

Want your translation in this list? Release a translation mod (i.e. a mod with only a Languages folder, _not a copy of this entire mod + the translation_) for this mod, and notify me of its existence, and I'll add it to the list.

## License

Licensed under either of

- Apache License, Version 2.0, ([LICENSE.Apache-2.0](LICENSE.Apache-2.0) or http://www.apache.org/licenses/LICENSE-2.0)
- MIT license ([LICENSE.MIT](LICENSE.MIT) or http://opensource.org/licenses/MIT)

at your option.

`SPDX-License-Identifier: Apache-2.0 OR MIT`

### Contribution

Unless you explicitly state otherwise, any contribution intentionally submitted
for inclusion in the work by you, as defined in the Apache-2.0 license, shall be
dual licensed as above, without any additional terms or conditions.
