# CLAUDE.md — RealisticOrbitalTrade

## Project overview

Replaces vanilla's instant orbital trading with a physical flow: accepting a deal starts a quest, a trade shuttle lands, colonists haul the sold goods aboard, the shuttle takes off, and payment and bought goods arrive on a second shuttle.

- `Source/RealisticOrbitalTrade/` — the mod. `Patch/` holds the Harmony patches, `Core/` the trade state (`TradeAgreement`, `TradeShipData`, `Settings`), `Quest/` the quest nodes and parts, `Comps/`, `Jobs/` and `WorkGivers/` the shuttle loading logic.
- `Source/RealisticOrbitalTrade.Tests/` — DevTools tests (`internal sealed` fixtures, `InternalsVisibleTo("RealisticOrbitalTrade.Tests")`). The project is empty on 1.4 and 1.5; tests only exist for 1.6.
- Supported versions: 1.4, 1.5, 1.6. Build each with `.vscode/build.sh <version>`.

### Trade flow

1. `TradeShip.TryOpenComms` (Harmony prefix) creates a `TradeAgreement` and sets `TradeShipData.tradeAgreementForQuest`.
2. `TradeDeal.TryExecute` (prefix/postfix) generates `ROT_TradeShipTransportShip` through `QuestNode_Root_TradeShipTransportShip`. Sold items are recorded on the agreement, not moved.
3. Shuttle(s) land; `CompTradeShuttle` and `WorkGiver_HaulMultipleToTransporter` get colonists to load them.
4. On takeoff `QuestPart_TransferItemsToTrader` moves the transporter's contents into the trader's stock.
5. The to-player shuttle delivers the payment; the quest ends and the trade agreement is ended.

## Writing end-to-end trade tests

`Source/RealisticOrbitalTrade.Tests/OrbitalTradeEndToEndTests.cs` is the reference; its `TradeScene` helper builds the colony and trader.

- **Drive the real path.** Call `TradeShip.TryOpenComms`, take the `TradeSession.deal` tradeables, `ForceTo` a negative count to sell, then `deal.TryExecute`. Setting fields on `TradeAgreement` directly skips the patches under test.
- **Advance time with `Find.TickManager.DoSingleTick()`** in a coroutine (`IEnumerator` test), ticking a batch and then `yield return null`. Loop on a condition (`quest.State`, shuttle spawned) with a tick timeout instead of a fixed tick count.
- **Scene needs:** a home area around the beacon, a powered `OrbitalTradeBeacon` (`CompPowerTrader.PowerOn = true`), goods placed near it, and colonists whose timetable is `Anything` for all 24 hours, so they haul without waiting for work hours.
- **Override settings for the test** (`Settings._useMinimumTradeThreshold = false`, `_deliverItemsViaDropPod = false`, `_activeTradePausesDepartureTimer = false`) and restore them in `Dispose`.
- **Any `Log.Warning`/`Log.Error` fails the test**, including ones from cleanup. `Dispose` must only end quests that are `QuestState.Ongoing`; ending a finished quest logs `Tried to resolve a historical quest`.
- **Assert on things, not counters:**
    - `TradeShip.CountHeldOf` doesn't include stacks added by `QuestPart_TransferItemsToTrader`; sum `TradeShip.Goods` stack counts by def instead.
    - `Map.resourceCounter` only counts items in storage; loose items read as 0. Sum `Map.listerThings.ThingsOfDef(def)` stack counts instead.
- **Expect vs failure lines:** `Expect.*` doesn't throw, so one run can list several failures. `AreEqual(expected, actual)` prints `Expected <expected> == <actual>`.
- **Debugging a failing run:** `Log.Message` output goes to `Player.log`; test results go to `.savedatafolder/1.6/Test.log`. Logging state each loop iteration when it changes shows where a value diverges. Remove it before finishing.
- Run only the new fixture with `runTests` plus `testFilter: "class == <FixtureName>"`, and rebuild with `.vscode/build.sh 1.6` before each relaunch since the game loads the built mod. Stop the running game before relaunching.

@CLAUDE.SHARED.md
