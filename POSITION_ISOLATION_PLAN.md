# Position Isolation Plan

## Root Cause (09/02/2026 Incident)

The **FTMO 1** bot (`Trading Panel 2.0 Server.cs`) was running on the same cTrader instance as the **Pepperstone US30** bot (`SkyAnalyst Automated Trader.cs`). Both bots share the same `Positions.Closed` event and `Positions` collection at the cTrader platform level.

**What happened:**
1. Pepperstone bot opened 3 positions: `MANUAL_TP1`, `MANUAL_TP2`, `MANUAL_TP3` (PIDs 296531162-64)
2. The FTMO bot's `OnStart()` detected these positions and started tracking them (its `DetectNewPositions` matches `_TP1`, `_TP2`, `_TP3`)
3. TP1 hit its take profit at 10:22:27
4. The FTMO bot's `OnPositionClosed` fired, counted it as a positive trade, hit `MaxPositiveTradesDay=1` (`posTrades=1/1`)
5. FTMO bot called `CloseAllPositions()` which closed **every** US30 position — including Pepperstone's TP2 and TP3

**Evidence from FTMO log** (`FTMO 1_BotLog_2026-02-09_10-46-24.log`):
```
FTMO 1: 10:09 => Opened Buy, Id=296531164, SL=49822, TP=51577.6
FTMO 1: 10:09 => Opened Buy, Id=296531163, SL=49822, TP=50300.8
FTMO 1: 10:09 => Opened Buy, Id=296531162, SL=49822, TP=50141.2
FTMO 1: 10:22 => Single => posTrades=1/1
FTMO 1: 10:22 => Daily + limit => stopping
```

---

## The Problem

Every bot in the system has the same fundamental flaw: **no position ownership**. Specifically:

| Method | Problem |
|--------|---------|
| `CloseAllPositions()` | Closes ALL positions on the symbol, not just its own |
| `OnPositionClosed()` | Reacts to ANY position closure on the account |
| `CheckTPModifications()` | Tracks ALL positions on the symbol |
| `CheckSLModifications()` | Tracks ALL positions on the symbol |
| `DetectNewPositions()` | Picks up positions from other bots if labels match pattern |
| Leftover detection in `OnStart()` | Detects other bots' positions as "leftovers" |

---

## Solution: Bot-Specific Label Prefix

Each bot instance tags every position with a unique prefix derived from the `AccountName` parameter.

**Label format:** `{AccountName}_TP1`, `{AccountName}_TP2`, `{AccountName}_TP3`

Examples:
- Pepperstone bot: `Pepperstone US30_TP1`, `Pepperstone US30_TP2`, `Pepperstone US30_TP3`
- FTMO bot: `FTMO 1_TP1`, `FTMO 1_TP2`, `FTMO 1_TP3`

Why `AccountName`:
- Already a required parameter on every bot instance
- Already unique per bot (users set it to identify the account)
- Human-readable in cTrader's position list
- No new parameters needed

---

## Files to Modify

| Priority | File | Bot Type |
|----------|------|----------|
| 1 | `SkyAnalyst Automated Trader.cs` | Main bot (manual + webhook) |
| 2 | `Trading Panel 2.0 Server.cs` | Server/broadcaster |
| 3 | `Trading Panel 2.0 PR Receiver.cs` | PR Receiver |
| 4 | `Trading Panel 2.0 Receiver.cs` | Standard Receiver |
| 5 | `Trading Panel Reversal.cs` | Reversal Receiver |

---

## Detailed Changes: `SkyAnalyst Automated Trader.cs`

### Change 1: Add label prefix field and helper method

**Location:** Field declarations (around line 618-630)

Add:
```csharp
private string _labelPrefix;
```

**Location:** `OnStart()` (around line 632, early in the method)

Add:
```csharp
_labelPrefix = AccountName + "_";
```

Add helper method:
```csharp
private bool IsOwnPosition(Position pos)
{
    return pos.SymbolName == SymbolName
        && pos.Label != null
        && pos.Label.StartsWith(_labelPrefix, StringComparison.OrdinalIgnoreCase);
}
```

---

### Change 2: `ExecuteManualTrade()` — use prefixed labels

**Location:** Lines 1471-1473

Before:
```csharp
ExecuteMarketOrderAsync(tradeType, SymbolName, pos1, "MANUAL_TP1", finalDist, tp1);
ExecuteMarketOrderAsync(tradeType, SymbolName, pos2, "MANUAL_TP2", finalDist, tp2);
ExecuteMarketOrderAsync(tradeType, SymbolName, pos3, "MANUAL_TP3", finalDist, tp3);
```

After:
```csharp
ExecuteMarketOrderAsync(tradeType, SymbolName, pos1, $"{_labelPrefix}TP1", finalDist, tp1);
ExecuteMarketOrderAsync(tradeType, SymbolName, pos2, $"{_labelPrefix}TP2", finalDist, tp2);
ExecuteMarketOrderAsync(tradeType, SymbolName, pos3, $"{_labelPrefix}TP3", finalDist, tp3);
```

---

### Change 3: `ExecuteWebhookTrade()` — use prefixed labels

**Location:** Around lines 1150-1200 (where webhook positions are opened)

All webhook position labels should also use `_labelPrefix`:
```csharp
// Before: labels like "tradeId_TP1", "tradeId_TP2", "tradeId_TP3"
// After:  labels like "{_labelPrefix}tradeId_TP1", etc.
```

Verify the exact label format used in `ExecuteWebhookTrade` and `ExecuteMarketOrderWithExactPrices` and prepend `_labelPrefix`.

---

### Change 4: `CloseAllPositions()` — only close own positions

**Location:** Lines 1492-1499

Before:
```csharp
public void CloseAllPositions()
{
    SendJsonMessage("Close All Positions", null, null, null, null, null, false, SymbolName, JsonSLFormat);

    foreach (var pos in Positions)
        if (pos.SymbolName == SymbolName)
            ClosePositionAsync(pos);
}
```

After:
```csharp
public void CloseAllPositions()
{
    SendJsonMessage("Close All Positions", null, null, null, null, null, false, SymbolName, JsonSLFormat);

    foreach (var pos in Positions)
        if (IsOwnPosition(pos))
            ClosePositionAsync(pos);
}
```

---

### Change 5: `OnPositionClosed()` — only react to own positions

**Location:** Lines 1519-1570

Before:
```csharp
if (_manualPositionOpen && pos.Label.StartsWith("TP", StringComparison.OrdinalIgnoreCase))
{
    _manualTradeCumulativeProfit += pos.NetProfit;
    var remain = Positions.Count(p => p.SymbolName == SymbolName
                           && p.Label.StartsWith("TP", StringComparison.OrdinalIgnoreCase)
                           && p.Quantity > 0);
```

After:
```csharp
if (_manualPositionOpen && pos.Label != null && pos.Label.StartsWith(_labelPrefix, StringComparison.OrdinalIgnoreCase))
{
    _manualTradeCumulativeProfit += pos.NetProfit;
    var remain = Positions.Count(p => IsOwnPosition(p) && p.Quantity > 0);
```

---

### Change 6: `CheckTPModifications()` — only track own positions

**Location:** Lines 1623-1648

Before:
```csharp
foreach (var pos in Positions.Where(p => p.SymbolName == SymbolName))
```

After:
```csharp
foreach (var pos in Positions.Where(p => IsOwnPosition(p)))
```

---

### Change 7: `CheckSLModifications()` — only track own positions

**Location:** Lines 1650-1675

Before:
```csharp
foreach (var pos in Positions.Where(p => p.SymbolName == SymbolName))
```

After:
```csharp
foreach (var pos in Positions.Where(p => IsOwnPosition(p)))
```

---

### Change 8: Leftover position detection in `OnStart()` — only detect own positions

**Location:** Lines 730-734

Before:
```csharp
bool leftoverPartial = Positions.Any(pos =>
    pos.SymbolName == SymbolName &&
    (pos.Label == "TP1Position" || pos.Label == "TP2Position" || pos.Label == "TP3Position") &&
    pos.Quantity > 0
);
```

After:
```csharp
bool leftoverPartial = Positions.Any(pos => IsOwnPosition(pos) && pos.Quantity > 0);
```

---

### Change 9: `MultiPositionPartialTPManager` — only detect own positions

**Location:** `DetectNewPositions()` (lines 2601-2636)

The manager needs the label prefix passed via constructor.

Constructor change:
```csharp
private readonly string _labelPrefix;

public MultiPositionPartialTPManager(Robot robot, ..., string labelPrefix)
{
    // existing params...
    _labelPrefix = labelPrefix;
}
```

`DetectNewPositions()` before:
```csharp
var newly = _robot.Positions
    .Where(p => p.SymbolName == _robot.SymbolName
        && (p.Label == "TP1Position" || p.Label == "TP2Position" || p.Label == "TP3Position" ||
            p.Label.Contains("_TP1") || p.Label.Contains("_TP2") || p.Label.Contains("_TP3"))
        && p.Quantity > 0
        && !_mpos.Any(x => x.Id == p.Id))
    .ToList();
```

`DetectNewPositions()` after:
```csharp
var newly = _robot.Positions
    .Where(p => p.SymbolName == _robot.SymbolName
        && p.Label != null
        && p.Label.StartsWith(_labelPrefix, StringComparison.OrdinalIgnoreCase)
        && p.Quantity > 0
        && !_mpos.Any(x => x.Id == p.Id))
    .ToList();
```

Also update `TradeGroupId` extraction to strip the prefix:
```csharp
string tradeGroupId = "MANUAL";
string labelWithoutPrefix = pos.Label.Substring(_labelPrefix.Length);
if (labelWithoutPrefix.Contains("_TP"))
{
    int underscoreIndex = labelWithoutPrefix.IndexOf("_TP");
    if (underscoreIndex > 0)
        tradeGroupId = labelWithoutPrefix.Substring(0, underscoreIndex);
}
```

Update the constructor call in `OnStart()`:
```csharp
_tradeManager = new MultiPositionPartialTPManager(
    this,
    TP1Percent, TP2Percent, TP3Percent,
    TP1R, TP2R, TP3R,
    TSL_Enabled, TSL_R_Trigger, TSL_R_Distance,
    _labelPrefix  // NEW parameter
);
```

---

### Change 10: `CountActiveTrades()` — verify isolation

**Location:** Around line 1870

This method counts active webhook trade groups. Verify it only counts positions with `_labelPrefix`. The existing implementation uses `_webhookTradePositions` dictionary which should already be isolated (only populated by this bot's webhook handler). But add a safety filter if it queries `Positions` directly.

---

### Change 11: Broadcast label mapping

When broadcasting to receivers, the labels sent in JSON should be **standardized** (without the prefix) so receivers can map them to their own prefixed labels.

**`BroadcastPositionClosure()`** — strip prefix before sending:
```csharp
string standardLabel = pos.Label.StartsWith(_labelPrefix) 
    ? pos.Label.Substring(_labelPrefix.Length) 
    : pos.Label;
msg["position_label"] = standardLabel;
```

**`BroadcastTPModification()` / `BroadcastSLModification()`** — same pattern.

---

## Detailed Changes: `Trading Panel 2.0 Server.cs`

Same pattern as above, but simpler (no webhook trade logic):

1. Add `_labelPrefix` field + `IsOwnPosition()` helper
2. `ExecuteManualTrade()` — change `"TP1Position"` → `$"{_labelPrefix}TP1"` (etc.)
3. `CloseAllPositions()` — filter by `IsOwnPosition()`
4. `OnPositionClosed()` — filter by `_labelPrefix` instead of `StartsWith("TP")`
5. `CheckTPModifications()` / `CheckSLModifications()` — filter by `IsOwnPosition()`
6. Leftover detection — filter by `IsOwnPosition()`
7. `MultiPositionPartialTPManager` — pass `_labelPrefix`
8. Broadcast methods — strip prefix before sending

---

## Detailed Changes: Receiver Bots

(`Trading Panel 2.0 PR Receiver.cs`, `Trading Panel 2.0 Receiver.cs`, `Trading Panel Reversal.cs`)

Same core changes plus:

1. `ExecuteReceivedTrade()` — use `$"{_labelPrefix}TP1"` instead of `"TP1Position"`
2. `ProcessPositionClosure()` — map incoming standardized label to own prefixed label:
   ```csharp
   string ownLabel = _labelPrefix + posLabel;  // e.g., "FTMO 1_" + "TP1" = "FTMO 1_TP1"
   var matchingPositions = Positions.Where(p => p.Label == ownLabel).ToList();
   ```
3. `ProcessTPModification()` / `ProcessSLModification()` — same label mapping

---

## Testing Checklist

- [ ] Bot A opens positions → labels contain Bot A's prefix
- [ ] Bot B opens positions → labels contain Bot B's prefix
- [ ] Bot A's `OnPositionClosed` ignores Bot B's position closures
- [ ] Bot A's `CloseAllPositions()` only closes Bot A's positions
- [ ] Bot A's `CheckTPModifications` only tracks Bot A's positions
- [ ] Bot A broadcasts closure → Receiver correctly maps to its own prefixed positions
- [ ] TSL manager only tracks positions from its own bot
- [ ] Daily trade counters only count own bot's positions
- [ ] Leftover detection on restart only finds own bot's positions
- [ ] `CheckHardStops` calls `CloseAllPositions()` which now only closes own positions
- [ ] UI "Close All" button only closes own bot's positions

---

## Risk Notes

- **`CheckHardStops` uses account-wide equity.** This is intentional — it's a global safety net. But now `CloseAllPositions()` only closes own positions, so if Bot A hits its drawdown limit, it won't kill Bot B's positions. Each bot protects itself independently.
- **Broadcast messages use standardized labels (no prefix).** This ensures receivers don't need to know the sender's `AccountName`. They map incoming labels to their own prefix.
- **Backward compatibility:** Old positions with labels like `TP1Position` or `MANUAL_TP1` won't be detected by the updated bots. This is fine — those positions should be manually closed before deploying the update, or a one-time migration check can be added to `OnStart()`.
