# Trading Panel Rv 2.0 - Implementation Plan

## Project Overview
Transform the Trading Panel 2.0 Receiver into **Trading Panel Rv 2.0** (Reverse) that automatically reverses trades from the broadcaster with inverted risk/reward mechanics. The Rv Receiver uses the broadcaster's TP levels as SL levels and the broadcaster's SL as the TP target, with position sizing calculated to achieve a fixed profit target.

**Key Design Decisions**:
- Bot name: "Trading Panel Rv 2.0"
- Trade Mode: Reverse only (Copy mode disabled but parameter kept for compatibility)
- Manual Trading: Enabled (uses existing Risk parameters)
- Received Trades: Use new Profit parameters for position sizing
- Simplification: Keep existing Risk system intact, add separate Profit system for reversed trades

---

## Core Concept: Profit Reversal Trading

### Standard Trade (Broadcaster)
- **Entry**: 400
- **Direction**: Buy
- **SL**: 380 (20 pips below entry)
- **TP1**: 420 (20 pips above, 1:1 R:R)
- **TP2**: 440 (40 pips above, 2:1 R:R)
- **TP3**: 460 (60 pips above, 3:1 R:R)
- **Risk**: $100
- **Potential Profit**: $100 (TP1), $200 (TP2), $300 (TP3)

### Reversed Trade (PR Receiver)
- **Entry**: 400
- **Direction**: Sell (reversed)
- **TP (Target)**: 380 (broadcaster's SL becomes our TP)
- **SL1**: 420 (broadcaster's TP1 becomes our SL1 for partial 1)
- **SL2**: 440 (broadcaster's TP2 becomes our SL2 for partial 2)
- **SL3**: 460 (broadcaster's TP3 becomes our SL3 for partial 3)
- **Target Profit**: $100 (fixed, user-defined)
- **Potential Loss**: $150 (if SL1 hit), $300 (if SL2 hit), $450 (if SL3 hit)

### Key Mechanics
1. **Direction**: Always opposite of broadcaster
2. **TP = Broadcaster's SL**: Maximum profit point
3. **SL = Broadcaster's TP**: Each partial has its own SL at the broadcaster's corresponding TP level
4. **Position Sizing**: Calculated so that if TP is hit, total profit = target profit amount
5. **Risk Multiplier**: Since we're betting against the broadcaster's R:R, our risk is amplified by the broadcaster's reward ratio

---

## Requirements Breakdown

### 1. Disable Copy Mode (Keep Reverse Only)
**Current State**: 
- `TradeModeParam` can be set to Copy or Reverse
- User can toggle between modes

**Required Changes**:
- Keep `TradeModeParam` parameter but set default to Reverse
- Add validation in `OnStart()` to force Reverse mode if user tries to set Copy
- Update status card to show "Mode: Reverse (Rv 2.0)"
- Add warning log if user attempts Copy mode
- Manual trading panel remains enabled (uses Risk parameters)

**Implementation**:
```csharp
protected override void OnStart()
{
    // Force Reverse mode
    if (TradeModeParam == TradeMode.Copy)
    {
        PrintLocal("WARNING: Copy mode is disabled in Trading Panel Rv 2.0. Forcing Reverse mode.");
        TradeModeParam = TradeMode.Reverse;
    }
    
    PrintLocal($"Trading Panel Rv 2.0 - Mode: {TradeModeParam} (Reverse Only)");
    PrintLocal("Manual trading enabled - uses Risk parameters");
    PrintLocal("Received trades use Target Profit parameters");
    // ... rest of OnStart
}
```

### 2. Add Separate Profit Parameters (Keep Risk for Manual Trading)
**Current State**:
- Parameters named "Risk", "Starting Risk", "Max Risk" used for manual trading
- Risk calculation determines position size based on potential loss

**Required Changes**:
- **Keep existing Risk parameters** for manual trading panel
- **Add new Profit parameters** for received trades (reversed positions)
- Default Profit mode to Dollar Amount
- Separate calculation methods for Risk (manual) vs Profit (received)

**New Parameters to Add**:
```csharp
[Parameter("Target Profit Mode", Group = "Profit Target (Received Trades)", DefaultValue = RiskCalculationMode.Dollar_Amount)]
public RiskCalculationMode TargetProfitMode { get; set; }

[Parameter("Starting Target Profit (%)", Group = "Profit Target (Received Trades)", DefaultValue = 1.0)]
public double StartingTargetProfitPercent { get; set; }

[Parameter("Max Target Profit (%)", Group = "Profit Target (Received Trades)", DefaultValue = 2.0)]
public double MaxTargetProfitPercent { get; set; }

[Parameter("Starting Target Profit ($)", Group = "Profit Target (Received Trades)", DefaultValue = 100.0)]
public double StartingTargetProfitDollar { get; set; }

[Parameter("Max Target Profit ($)", Group = "Profit Target (Received Trades)", DefaultValue = 200.0)]
public double MaxTargetProfitDollar { get; set; }
```

**Keep Existing Risk Parameters**:
- `RiskMode` - for manual trading
- `StartingRiskPercent` - for manual trading
- `MaxRiskPercent` - for manual trading
- `StartingRiskDollar` - for manual trading
- `MaxRiskDollar` - for manual trading

---

### 3. Invert TP/SL Logic for Reversed Trades

#### 3.1 Entry Execution Logic
**Current State**:
- Receives trade with SL and TP levels
- Reverses direction
- Applies SL as SL and TP as TP (with reversal calculations)

**Required Changes**:
- **Broadcaster's SL → PR Receiver's TP (shared across all partials)**
- **Broadcaster's TP1 → PR Receiver's SL1 (for partial 1)**
- **Broadcaster's TP2 → PR Receiver's SL2 (for partial 2)**
- **Broadcaster's TP3 → PR Receiver's SL3 (for partial 3)**

**Key Calculation**:
```
Target Profit = $100 (user setting)
Distance to TP = |Entry - Broadcaster's SL| in pips
Position Size = Target Profit / (Distance to TP in pips × Pip Value)
```

**Risk Calculation per Partial**:
```
For TP1 Partial (30% of position):
  Distance to SL1 = |Entry - Broadcaster's TP1| in pips
  Risk for this partial = Position Size × 0.30 × Distance to SL1 × Pip Value
  
For TP2 Partial (30% of position):
  Distance to SL2 = |Entry - Broadcaster's TP2| in pips
  Risk for this partial = Position Size × 0.30 × Distance to SL2 × Pip Value
  
For TP3 Partial (40% of position):
  Distance to SL3 = |Entry - Broadcaster's TP3| in pips
  Risk for this partial = Position Size × 0.40 × Distance to SL3 × Pip Value
```

#### 3.2 Position Opening Logic
**Implementation Steps**:

1. **Extract Broadcaster's Levels**:
   ```csharp
   double broadcasterSL = slPrice.Value; // or calculate from slPips
   double broadcasterTP1 = tp1Price;
   double broadcasterTP2 = tp2Price;
   double broadcasterTP3 = tp3Price;
   ```

2. **Calculate PR Receiver's Levels**:
   ```csharp
   // Our TP is their SL
   double prReceiverTP = broadcasterSL;
   
   // Our SLs are their TPs (each partial gets its own SL)
   double prReceiverSL1 = broadcasterTP1;
   double prReceiverSL2 = broadcasterTP2;
   double prReceiverSL3 = broadcasterTP3;
   ```

3. **Calculate Position Size Based on Target Profit**:
   ```csharp
   double targetProfit = GetDynamicProfitAmount(); // Renamed from GetDynamicRiskAmount
   double entryPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
   
   // Distance from entry to TP (broadcaster's SL)
   double tpDistancePips = Math.Abs(entryPrice - prReceiverTP) / Symbol.PipSize;
   
   // Calculate total volume needed to achieve target profit
   double totalVolumeUnits = targetProfit / (tpDistancePips * Symbol.PipValue);
   totalVolumeUnits = Symbol.NormalizeVolumeInUnits(totalVolumeUnits, RoundingMode.ToNearest);
   ```

4. **Split Volume Across Partials**:
   ```csharp
   double pos1Volume = totalVolumeUnits * (TP1Percent / 100.0);
   double pos2Volume = totalVolumeUnits * (TP2Percent / 100.0);
   double pos3Volume = totalVolumeUnits * (TP3Percent / 100.0);
   
   // Normalize each
   pos1Volume = Symbol.NormalizeVolumeInUnits(pos1Volume, RoundingMode.ToNearest);
   pos2Volume = Symbol.NormalizeVolumeInUnits(pos2Volume, RoundingMode.ToNearest);
   pos3Volume = Symbol.NormalizeVolumeInUnits(pos3Volume, RoundingMode.ToNearest);
   ```

5. **Execute Orders with Individual SLs**:
   ```csharp
   // Each partial has the SAME TP but DIFFERENT SL
   ExecuteMarketOrderWithIndividualLevels(actualType, symbol, pos1Volume, "TP1Position", prReceiverSL1, prReceiverTP);
   ExecuteMarketOrderWithIndividualLevels(actualType, symbol, pos2Volume, "TP2Position", prReceiverSL2, prReceiverTP);
   ExecuteMarketOrderWithIndividualLevels(actualType, symbol, pos3Volume, "TP3Position", prReceiverSL3, prReceiverTP);
   ```

6. **New Helper Method**:
   ```csharp
   private void ExecuteMarketOrderWithIndividualLevels(TradeType tradeType, string symbol, double volume, 
                                                        string label, double slPrice, double tpPrice)
   {
       double entryPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
       
       // Calculate SL distance in pips
       double slDistancePips = Math.Abs(entryPrice - slPrice) / Symbol.PipSize;
       
       // Calculate TP distance in pips
       double tpDistancePips = Math.Abs(entryPrice - tpPrice) / Symbol.PipSize;
       
       var result = ExecuteMarketOrder(tradeType, symbol, volume, label, slDistancePips, tpDistancePips);
       
       if (result.IsSuccessful)
       {
           PrintLocal($"PR Position opened: {label}, Vol={volume}, SL={slPrice:F5} ({slDistancePips:F1}p), TP={tpPrice:F5} ({tpDistancePips:F1}p)");
           
           // Log the risk for this partial
           double riskForPartial = volume * slDistancePips * Symbol.PipValue;
           PrintLocal($"  Risk for {label}: ${riskForPartial:F2}");
       }
       else
       {
           PrintLocal($"Failed to open PR {label}: {result.Error}");
       }
   }
   ```

---

### 4. TP/SL Modification Handling

#### 4.1 TP Modification (Broadcaster moves TP)
**Current Behavior**:
- Broadcaster moves TP1 from 420 to 425
- Receiver moves its TP1 from 420 to 425 (or reverses to 375 if in reverse mode)

**Required PR Behavior**:
- Broadcaster moves TP1 from 420 to 425
- **PR Receiver moves SL1 from 420 to 425** (broadcaster's TP is our SL)
- This affects only the partial with label "TP1Position"

**Implementation**:
```csharp
private void ProcessTPModification(Dictionary<string, JsonElement> message)
{
    try
    {
        string posLabel = message["position_label"].GetString();
        string tradeTypeStr = message["trade_type"].GetString();
        double tpPrice = message["tp_price"].GetDouble();
        double tpPipDiff = message["tp_pip_diff"].GetDouble();
        
        PrintLocal($"Received TP Mod (PR Mode - will modify SL): {posLabel}, Price={tpPrice:F5}, PipDiff={tpPipDiff:F1}");
        
        var matchingPositions = Positions.Where(p => 
            p.SymbolName == SymbolName && 
            p.Label == posLabel).ToList();
        
        if (matchingPositions.Count == 0)
        {
            PrintLocal($"No matching position for: {posLabel}");
            return;
        }
        
        // In PR mode, broadcaster's TP becomes our SL
        double newSL = 0;
        TradeType originalType = Enum.Parse<TradeType>(tradeTypeStr);
        TradeType actualType = (originalType == TradeType.Buy) ? TradeType.Sell : TradeType.Buy;
        
        if (TPModMode == TPModificationMode.Price)
        {
            // Broadcaster's TP price becomes our SL price directly
            // Since we're reversed, their TP is on the opposite side of entry
            newSL = tpPrice;
            PrintLocal($"PR Mode: Broadcaster's TP → Our SL: {newSL:F5}");
        }
        else // Pip_Diff mode
        {
            // Calculate new SL from pip difference
            double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
            newSL = CalculateSLFromPips(actualType, myCurrentPrice, tpPipDiff);
            PrintLocal($"PR Mode: Broadcaster's TP pips → Our SL: {tpPipDiff:F1}p = {newSL:F5}");
        }
        
        // Modify the SL (not TP) for this position
        foreach (var pos in matchingPositions)
        {
            ModifyPositionSL(pos, newSL);
        }
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing TP mod in PR mode: {ex.Message}");
    }
}
```

#### 4.2 SL Modification (Broadcaster moves SL)
**Current Behavior**:
- Broadcaster moves SL from 380 to 385
- Receiver moves its SL from 380 to 385 (or reverses calculation)

**Required PR Behavior**:
- Broadcaster moves SL from 380 to 385
- **PR Receiver moves TP from 380 to 385** (broadcaster's SL is our TP)
- This affects **ALL partials** since they share the same TP

**Implementation**:
```csharp
private void ProcessSLModification(Dictionary<string, JsonElement> message)
{
    try
    {
        string posLabel = message["position_label"].GetString();
        string tradeTypeStr = message["trade_type"].GetString();
        double slPrice = message["sl_price"].GetDouble();
        double slPipDiff = message["sl_pip_diff"].GetDouble();
        
        PrintLocal($"Received SL Mod (PR Mode - will modify TP for all partials): Price={slPrice:F5}, PipDiff={slPipDiff:F1}");
        
        // In PR mode, when broadcaster moves SL, we need to move TP for ALL partials
        var allPartials = Positions.Where(p => 
            p.SymbolName == SymbolName && 
            (p.Label == "TP1Position" || p.Label == "TP2Position" || p.Label == "TP3Position")).ToList();
        
        if (allPartials.Count == 0)
        {
            PrintLocal($"No PR positions found to modify TP");
            return;
        }
        
        // In PR mode, broadcaster's SL becomes our TP
        double newTP = 0;
        TradeType originalType = Enum.Parse<TradeType>(tradeTypeStr);
        TradeType actualType = (originalType == TradeType.Buy) ? TradeType.Sell : TradeType.Buy;
        
        if (SLModMode == SLModificationMode.Price)
        {
            // Broadcaster's SL price becomes our TP price directly
            newTP = slPrice;
            PrintLocal($"PR Mode: Broadcaster's SL → Our TP (all partials): {newTP:F5}");
        }
        else // Pip_Diff mode
        {
            // Calculate new TP from pip difference
            double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
            newTP = CalculateTPFromPips(actualType, myCurrentPrice, slPipDiff);
            PrintLocal($"PR Mode: Broadcaster's SL pips → Our TP: {slPipDiff:F1}p = {newTP:F5}");
        }
        
        // Modify the TP (not SL) for ALL partials
        foreach (var pos in allPartials)
        {
            ModifyPositionTP(pos, newTP);
            PrintLocal($"  Modified TP for {pos.Label} to {newTP:F5}");
        }
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing SL mod in PR mode: {ex.Message}");
    }
}
```

---

### 5. Position Closure Synchronization

#### 5.1 Broadcaster Closes Position
**Requirement**: When broadcaster closes a position (manually, TP hit, or SL hit), the corresponding PR position should close immediately.

**Current Implementation**:
```csharp
private void ProcessPositionClosure(Dictionary<string, JsonElement> message)
{
    try
    {
        string posLabel = message["position_label"].GetString();
        
        PrintLocal($"Received Position Closure: {posLabel}");
        
        var matchingPositions = Positions.Where(p => 
            p.SymbolName == SymbolName && 
            p.Label == posLabel).ToList();
        
        if (matchingPositions.Count == 0)
        {
            PrintLocal($"No matching position to close: {posLabel}");
            return;
        }
        
        foreach (var pos in matchingPositions)
        {
            ClosePositionAsync(pos);
            PrintLocal($"Closing PR position: {pos.Id} ({pos.Label})");
        }
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing position closure: {ex.Message}");
    }
}
```

**Analysis**: Current implementation is correct. When broadcaster sends "ClosePosition" with a label, the PR receiver will close the matching position immediately, regardless of whether its own TP/SL would have been hit.

**Race Condition Handling**:
- If PR position's TP/SL is hit first, it closes naturally
- If broadcaster closes first, the closure command closes the PR position
- No additional logic needed - whichever happens first wins

---

### 6. Pip Difference Mode Verification

#### 6.1 Current Pip Diff Logic for TP Modification
```csharp
// In ProcessTPModification, Pip_Diff mode:
double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
newTP = CalculateTPFromPips(actualType, myCurrentPrice, tpPipDiff);
```

**Issue**: This calculates TP from current price + pip difference. In PR mode, we need to calculate SL from current price + pip difference.

**Fix**: Already addressed in section 4.1 above.

#### 6.2 Current Pip Diff Logic for SL Modification
```csharp
// In ProcessSLModification, Pip_Diff mode:
double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
newSL = CalculateSLFromPips(actualType, myCurrentPrice, slPipDiff);
```

**Issue**: This calculates SL from current price - pip difference. In PR mode, we need to calculate TP from current price - pip difference.

**Fix**: Already addressed in section 4.2 above.

---

## Implementation Checklist

### Phase 1: Parameter Renaming and Mode Locking
- [ ] Remove `TradeModeParam` parameter
- [ ] Hardcode `_tradeMode = TradeMode.Reverse`
- [ ] Rename all Risk parameters to Profit parameters
- [ ] Update parameter defaults (Dollar_Amount mode, $100 default)
- [ ] Update status card to show "Profit Reversal (PR)" mode
- [ ] Update UI labels in TradingPanel
- [ ] Rename `GetDynamicRiskAmount()` to `GetDynamicProfitAmount()`
- [ ] Rename `CalculateDynamicRiskPercent()` to `CalculateDynamicProfitPercent()`
- [ ] Rename `CalculateDynamicRiskDollar()` to `CalculateDynamicProfitDollar()`

### Phase 2: Core Trade Execution Logic
- [ ] Create `ExecuteMarketOrderWithIndividualLevels()` method
- [ ] Modify `ExecuteReceivedTrade()` to implement PR logic:
  - [ ] Extract broadcaster's SL and TP levels
  - [ ] Invert: broadcaster's SL → our TP
  - [ ] Invert: broadcaster's TPs → our SLs (individual per partial)
  - [ ] Calculate position size based on target profit and distance to TP
  - [ ] Split volume across partials
  - [ ] Execute orders with individual SL levels but shared TP
- [ ] Add detailed logging for risk per partial
- [ ] Add total risk calculation and logging

### Phase 3: TP/SL Modification Logic
- [ ] Modify `ProcessTPModification()` for PR mode:
  - [ ] Broadcaster's TP modification → Modify our SL
  - [ ] Handle Price mode correctly
  - [ ] Handle Pip_Diff mode correctly
  - [ ] Update only the specific partial position
- [ ] Modify `ProcessSLModification()` for PR mode:
  - [ ] Broadcaster's SL modification → Modify our TP
  - [ ] Handle Price mode correctly
  - [ ] Handle Pip_Diff mode correctly
  - [ ] Update ALL partial positions (shared TP)

### Phase 4: Testing and Validation
- [ ] Test with 2 TP levels (TP1, TP2 only)
- [ ] Test with 3 TP levels (TP1, TP2, TP3)
- [ ] Test TP modification in Price mode
- [ ] Test TP modification in Pip_Diff mode
- [ ] Test SL modification in Price mode
- [ ] Test SL modification in Pip_Diff mode
- [ ] Test position closure from broadcaster
- [ ] Test natural TP hit (should close all partials)
- [ ] Test natural SL hit (should close specific partial)
- [ ] Verify profit calculation is accurate
- [ ] Verify risk calculation per partial is accurate

### Phase 5: Documentation and Cleanup
- [ ] Update bot title to "Trading Panel 2.0 PR Receiver"
- [ ] Add comments explaining PR logic
- [ ] Update log messages to be PR-specific
- [ ] Remove unused Copy mode logic
- [ ] Clean up any dead code

---

## Risk/Reward Example Calculation

### Scenario: Broadcaster Opens Buy Trade
- **Entry**: 42000
- **SL**: 41980 (20 pips below)
- **TP1**: 42020 (20 pips above, 1:1)
- **TP2**: 42040 (40 pips above, 2:1)
- **TP3**: 42100 (100 pips above, 5:1)
- **Broadcaster's Risk**: $100

### PR Receiver (Reversed)
- **Entry**: 42000
- **Direction**: Sell (reversed)
- **TP**: 41980 (broadcaster's SL, 20 pips below)
- **SL1**: 42020 (broadcaster's TP1, 20 pips above) - for 30% of position
- **SL2**: 42040 (broadcaster's TP2, 40 pips above) - for 30% of position
- **SL3**: 42100 (broadcaster's TP3, 100 pips above) - for 40% of position
- **Target Profit**: $100

### Position Sizing
```
Distance to TP = 20 pips
Target Profit = $100
Pip Value = $10 (example for US30)

Total Volume = $100 / (20 pips × $10) = 0.5 lots

Partial 1 (30%): 0.15 lots, SL at 42020 (20 pips risk)
  Risk = 0.15 × 20 × $10 = $30

Partial 2 (30%): 0.15 lots, SL at 42040 (40 pips risk)
  Risk = 0.15 × 40 × $10 = $60

Partial 3 (40%): 0.20 lots, SL at 42100 (100 pips risk)
  Risk = 0.20 × 100 × $10 = $200

Total Risk if all SLs hit = $290
```

### Outcome Scenarios
1. **TP Hit (41980 reached)**: Win $100 (target achieved)
2. **SL1 Hit (42020 reached)**: Lose $30, remaining positions still open
3. **SL2 Hit (42040 reached)**: Lose $30 + $60 = $90, TP3 still open
4. **SL3 Hit (42100 reached)**: Lose $30 + $60 + $200 = $290

---

## Notes and Considerations

### 1. Position Tracking
- PR positions use same labels as regular receiver: "TP1Position", "TP2Position", "TP3Position"
- This allows broadcaster's closure commands to match correctly
- No changes needed to position tracking logic

### 2. Manual Trading
- Manual trading panel should be disabled or clearly marked as "Not applicable in PR mode"
- PR mode is designed for automated reversal only
- Consider hiding or disabling the manual trading panel

### 3. Risk Disclosure
- Add clear warnings in logs about amplified risk
- Log total risk exposure when opening positions
- Consider adding a parameter for max total risk limit

### 4. Partial Closure Behavior
- If broadcaster closes TP1Position, PR receiver closes its TP1Position
- Remaining partials (TP2, TP3) stay open with their individual SLs
- TP remains the same for all remaining positions

### 5. Dynamic Profit Scaling
- Keep the dynamic scaling logic but rename it
- Profit scales up with account growth
- Profit scales down with account drawdown
- Same logic, just renamed from "risk" to "profit"

---

## Testing Scenarios

### Test 1: Basic PR Trade Execution
1. Broadcaster opens Buy at 42000, SL 41980, TP1 42020, TP2 42040, TP3 42100
2. Verify PR opens Sell at 42000
3. Verify TP = 41980 for all partials
4. Verify SL1 = 42020, SL2 = 42040, SL3 = 42100
5. Verify position sizes sum to target profit / (20 pips × pip value)

### Test 2: TP Modification
1. Broadcaster moves TP1 from 42020 to 42025
2. Verify PR moves SL1 from 42020 to 42025
3. Verify SL2 and SL3 remain unchanged
4. Verify TP remains unchanged

### Test 3: SL Modification
1. Broadcaster moves SL from 41980 to 41985
2. Verify PR moves TP from 41980 to 41985 for ALL partials
3. Verify all SLs remain unchanged

### Test 4: Broadcaster Closes Position
1. Broadcaster closes TP1Position
2. Verify PR closes its TP1Position
3. Verify TP2Position and TP3Position remain open

### Test 5: Natural TP Hit
1. Price reaches 41980 (PR's TP)
2. Verify all PR positions close
3. Verify profit = target profit amount

### Test 6: Natural SL Hit
1. Price reaches 42020 (PR's SL1)
2. Verify only TP1Position closes
3. Verify TP2Position and TP3Position remain open
4. Verify loss = calculated risk for TP1Position

---

## File Changes Summary

### Files to Modify
1. **Trading Panel 2.0 PR Receiver.cs** (main file)
   - Update title and copyright header
   - Remove TradeModeParam parameter
   - Rename all Risk parameters to Profit
   - Modify ExecuteReceivedTrade() for PR logic
   - Modify ProcessTPModification() for PR logic
   - Modify ProcessSLModification() for PR logic
   - Create ExecuteMarketOrderWithIndividualLevels() method
   - Update status card display
   - Update UI labels

### Files to Create
- None (all changes in existing file)

### Files to Reference
- **SkyAnalyst Automated Trader.cs** (broadcaster) - for understanding message format
- **Trading Panel 2.0 Receiver.cs** (original) - for reference

---

## End of Implementation Plan
