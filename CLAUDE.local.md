# CLAUDE.local.md — RealisticOrbitalTrade

## Project overview

Replaces vanilla's instant orbital trading with a physical flow: accepting a deal starts a quest, a trade shuttle lands, colonists haul the sold goods aboard, the shuttle takes off, and payment and bought goods arrive on a second shuttle.

- `Source/RealisticOrbitalTrade/` — the mod. `Patch/` holds the Harmony patches, `Core/` the trade state (`TradeAgreement`, `TradeShipData`, `Settings`), `Quest/` the quest nodes and parts, `Comps/`, `Jobs/` and `WorkGivers/` the shuttle loading logic.
- `Source/RealisticOrbitalTrade.Tests/` — DevTools tests (`internal sealed` fixtures, `InternalsVisibleTo("RealisticOrbitalTrade.Tests")`). The project is empty on 1.4 and 1.5; tests only exist for 1.6 and 1.7.
- Supported versions: 1.4, 1.5, 1.6, 1.7. Build each with `.vscode/build.sh <version>`.

### Trade flow

1. `TradeShip.TryOpenComms` (Harmony prefix) creates a `TradeAgreement` and sets `TradeShipData.tradeAgreementForQuest`.
2. `TradeDeal.TryExecute` (prefix/postfix) generates `ROT_TradeShipTransportShip` through `QuestNode_Root_TradeShipTransportShip`. Sold items are recorded on the agreement, not moved.
3. Shuttle(s) land; `CompTradeShuttle` and `WorkGiver_HaulMultipleToTransporter` get colonists to load them.
4. On takeoff `QuestPart_TransferItemsToTrader` moves the transporter's contents into the trader's stock.
5. The to-player shuttle delivers the payment; the quest ends and the trade agreement is ended.

## Writing end-to-end trade tests

`Source/RealisticOrbitalTrade.Tests/TradeScene.cs` is the shared harness: it builds the colony and trader, sells goods through the real dialog (`SellAndWaitForCompletion`), and makes the assertions every finished sale shares (`ExpectSaleCompleted`). `OrbitalTradeEndToEndTests.cs` (one shuttle) and `MultiShuttleEndToEndTests.cs` (sale split across shuttles) are the references; a new test places goods, sells, and adds only its own assertions. Extend `TradeScene` rather than copying setup into a fixture.

- **Scenarios** (cancelling, renegotiating, expiring, a second trader, selling again, another mod, Find / Trade): `TradeScene`'s methods drive them, and the `*EndToEndTests` fixture named after the scenario shows how. Find / Trade tests live in `Source/RealisticOrbitalTrade.FindTrade.Tests/` and use `FindTradeScene`.
- **Settings:** `TradeScene` applies the mod's defaults; pass your own as `SettingsSnapshot.Defaults with { MultiItemShuttleLoading = false }`.
- **Make a sale heavy by lowering the shuttle capacity** (`TradeScene.LimitShuttleCapacity`) instead of selling a heavy item: shuttles carry 2000 kg, so 1 kg items like uranium would need thousands of hauling trips.
- **Tests share one world and map.** `TradeScene.Dispose` puts back everything a test touched; anything new a test changes on the shared state goes into `Dispose` too. Autosave stays off while a test runs: saving the shared world logs warnings about what earlier tests removed.
- **Let frames pass only through `TradeScene`'s tick helpers**, never with a bare `yield return null` in a test: the scene keeps its own `Rand` state and sets it aside while a frame passes, since the game's per-frame work draws from `Rand` a varying number of times and would otherwise make runs differ.
- **Logged warnings and errors fail the test**, including ones from cleanup. Allow them per test method, never in `Configs/UnitTestConfig.xml`. `Dispose` must only end quests that are `QuestState.Ongoing`; ending a finished quest logs `Tried to resolve a historical quest`.
- **Assert on things, not counters:**
    - `TradeShip.CountHeldOf` doesn't include stacks added by `QuestPart_TransferItemsToTrader`; sum `TradeShip.Goods` stack counts by def instead.
    - `Map.resourceCounter` only counts items in storage; loose items read as 0. Sum `Map.listerThings.ThingsOfDef(def)` stack counts instead.
- **Running:** `gameId` is `RealisticOrbitalTrade`; the game runs 1.7 unless `launchInputs` adds `"rimworldVersion": "1.6"`. Find / Trade tests run under the `FindTrade` plan, Bulk Load ones under `BulkLoad`. Rebuild with `.vscode/build.sh <version>` before each relaunch, since the game loads the built mod.
