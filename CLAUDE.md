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

`Source/RealisticOrbitalTrade.Tests/TradeScene.cs` is the shared harness: it builds the colony and trader, sells goods through the real dialog (`SellAndWaitForCompletion`), and makes the assertions every finished sale shares (`ExpectSaleCompleted`). `OrbitalTradeEndToEndTests.cs` (one shuttle) and `MultiShuttleEndToEndTests.cs` (sale split across shuttles) are the references; a new test places goods, sells, and adds only its own assertions. Extend `TradeScene` rather than copying setup into a fixture.

- **Drive the real path.** Call `TradeShip.TryOpenComms`, take the `TradeSession.deal` tradeables, `ForceTo` a negative count to sell, then `deal.TryExecute`. Setting fields on `TradeAgreement` directly skips the patches under test.
- **Advance time with `Find.TickManager.DoSingleTick()`** in a coroutine (`IEnumerator` test), ticking a batch and then `yield return null`. Loop on a condition (`quest.State`, shuttle spawned) with a tick timeout instead of a fixed tick count.
- **Scene needs:** a home area around the beacon, a powered `OrbitalTradeBeacon` (`CompPowerTrader.PowerOn = true`), goods placed near it, and colonists whose timetable is `Anything` for all 24 hours, so they haul without waiting for work hours.
- **Every setting is set explicitly.** `TradeScene` applies a full `SettingsSnapshot` (the mod's defaults unless the test passes its own, e.g. `SettingsSnapshot.Defaults with { MultiItemShuttleLoading = false }`) and restores the player's values in `Dispose`, so a run never depends on the saved config file.
- **Interfere with a trade in flight** with `SellAndWaitForLanding`, `WaitForPartialLoad`, `CancelTradeFromShuttle` (checked by `ExpectSaleCancelled`) and `RenegotiateSale(newCount)` (which updates the sale, so `ExpectSaleCompleted` expects the new amount and payment). `TradeCancellationEndToEndTests` and `TradeRenegotiationEndToEndTests` are the references.
- **Expire a trade** with `ShortenTraderStay` (before selling; use `MinTicksUntilDepartureBeforeGraceTime = 0` so no grace time is added) or `RunDownDepartureTimer` (after `WaitForPartialLoad`). `StopColonistsHauling` keeps anything from loading. With part loaded, `WaitForDepartureRenegotiation` and `AcceptDepartureRenegotiation` follow the trader's offer to renegotiate down to what is loaded (`TradeExpirationEndToEndTests`).
- **Tests that need another mod** use `[LoadIfModsActive(...)]` plus a plan in `TestPlans/` that loads it (`TestPlans/BulkLoad.xml`); run it with the `testPlan` launch input.
- **Generate colonists that can trade and haul.** A random colonist can have the price-improvement stat disabled, which logs an error and fails the test; `TradeScene` regenerates until the colonist can negotiate and haul.
- **Make a sale heavy by lowering the shuttle capacity** (`TradeScene.LimitShuttleCapacity`) instead of selling a heavy item: shuttles carry 2000 kg, so 1 kg items like uranium would need thousands of hauling trips. `Dispose` restores the capacity.
- **Tests share one world and map.** DevTools regenerates them only for a fixture with custom generation settings, so `TradeScene.Dispose` puts back everything a test touched (quests, letters, home area, items, shuttles, pawns, settings, shuttle capacity). Anything new a test changes on the shared state goes into `Dispose` too.
- **Any `Log.Warning`/`Log.Error` fails the test**, including ones from cleanup. `Dispose` must only end quests that are `QuestState.Ongoing`; ending a finished quest logs `Tried to resolve a historical quest`.
- **Assert on things, not counters:**
    - `TradeShip.CountHeldOf` doesn't include stacks added by `QuestPart_TransferItemsToTrader`; sum `TradeShip.Goods` stack counts by def instead.
    - `Map.resourceCounter` only counts items in storage; loose items read as 0. Sum `Map.listerThings.ThingsOfDef(def)` stack counts instead.
- **Expect vs failure lines:** `Expect.*` doesn't throw, so one run can list several failures. `AreEqual(expected, actual)` prints `Expected <expected> == <actual>`.
- **Debugging a failing run:** `Log.Message` output goes to `Player.log`; test results go to `.savedatafolder/1.6/Test.log`. Logging state each loop iteration when it changes shows where a value diverges. Remove it before finishing.
- **Tests only run through launch inputs.** Start the game with the GABS `games_start` tool, `gameId: "RealisticOrbitalTrade"`, and `launchInputs`; a start without `runTests` (or `testPlan`) just opens the game. `{"runTests": true}` runs the whole suite, `{"runTests": true, "testFilter": "class == <FixtureName>"}` (or `"method == <TestName>"`) a subset, and `{"testPlan": "<name>"}` a plan from `TestPlans/`.
- Run only the new fixture with `runTests` plus `testFilter: "class == <FixtureName>"`, and rebuild with `.vscode/build.sh 1.6` before each relaunch since the game loads the built mod. Stop the running game before relaunching.
- The runner exits when it finishes, so `games_start` can report exit code 1 for a failing run; the result is in `.savedatafolder/1.6/Test.log`. Wait for its `Test runner finished. Result:` line with a backgrounded `until grep -q` loop instead of sleeping.

@CLAUDE.SHARED.md
