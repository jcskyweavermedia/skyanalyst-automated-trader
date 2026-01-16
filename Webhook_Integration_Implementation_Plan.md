# Webhook Integration Implementation Plan
## SkyAnalyst Automated Trader - Webhook Receiver

---

## Executive Summary

This plan details the integration of webhook-based automated trading into the existing `SkyAnalyst Automated Trader.cs`. The bot will receive AI-generated trade signals via webhooks and execute them automatically using the panel's risk management settings while respecting the webhook's TP levels.

---

## Webhook Payload Structure (from Integration Guide)

### Incoming JSON Format
```json
{
  "alert_type": "entry_approved",
  "alert_id": "uuid",
  "trade_id": "uuid",
  "instrument": "XAUUSD-Pepperstone",
  "direction": "LONG" | "SHORT",
  "time": "ISO8601",
  "entry_zone": {
    "min": 2625.00,
    "max": 2628.00,
    "mid": 2626.50,
    "is_zone": true,
    "aggressive": 2628.00,
    "conservative": 2625.00
  },
  "stop_loss": {
    "min": 2608.50,
    "max": 2610.50,
    "mid": 2609.50,
    "is_zone": true,
    "tight": 2610.50,
    "wide": 2608.50
  },
  "tp1": {
    "min": 2640.00,
    "max": 2640.00,
    "mid": 2640.00,
    "is_zone": false,
    "early": 2640.00,
    "full": 2640.00
  },
  "tp2": { /* same structure */ },
  "tp3": null,
  "ai_decision": "string",
  "confidence": 72
}
```

---

## Implementation Requirements

### 1. **Webhook Server Setup**
- **Default Port**: 8050 (configurable parameter)
- **Endpoint**: `/webhook` (new endpoint, separate from existing `/newtrade`)
- **Method**: POST
- **Content-Type**: application/json
- **Response**: 200 OK within 5 seconds

### 2. **Broker Price Feed Validation**
- **Purpose**: Ensure webhook instrument broker matches MetaTrader broker
- **Use Case**: US indexes where broker price feeds vary
- **Setting Name**: "Correct Price Feed Broker Check" (or "Broker Match Validation")
- **Default**: ON (enabled)
- **Logic**: 
  - Extract broker suffix from webhook `instrument` field (e.g., "XAUUSD-Pepperstone")
  - Compare with current broker name in MetaTrader
  - If mismatch and setting is ON, reject webhook and log warning
  - If setting is OFF, proceed regardless

### 3. **Trade ID Tracking (Duplicate Prevention)**
- **Purpose**: Prevent duplicate trade execution from same webhook
- **Data Structure**: Dictionary/HashSet to store processed `trade_id` values
- **Logic**:
  - Check if `trade_id` exists in tracking collection
  - If exists, reject webhook with log message "Duplicate trade_id detected"
  - If new, add to collection and proceed
  - Clean up old trade_ids periodically (e.g., daily reset or after 24 hours)
- **Multi-Trade Support**: Bot can manage multiple active trades simultaneously
- **Position Tracking**: Link webhook `trade_id` to MetaTrader position IDs for lifecycle management

### 4. **Symbol Filtering**
- **Setting Type**: Dropdown/ComboBox with options:
  - 15 predefined symbols (US30, NAS100, XAUUSD, EURUSD, etc.)
  - "Accept Any Symbol"
- **Default**: "US30-Pepperstone"
- **Logic**:
  - If specific symbol selected, only process webhooks matching that symbol
  - If "Accept Any", process all incoming webhooks
  - Extract base symbol from webhook `instrument` (before broker suffix)
- **Symbol Mapping**: 
  - Map webhook symbols to MetaTrader instrument names
  - Example: "US30-Pepperstone" → "US30" or "US30.cash" depending on broker
  - Create configurable mapping dictionary

### 5. **Zone Selection Settings**
Two new settings to control which part of zones to use:

#### A. Stop Loss Zone Selection
- **Setting Name**: "SL Zone Preference"
- **Options**: 
  - "Tight" (uses `stop_loss.tight`)
  - "Wide" (uses `stop_loss.wide`)
- **Default**: "Wide"
- **Logic**: When `stop_loss.is_zone == true`, use selected preference

#### B. Take Profit Zone Selection
- **Setting Name**: "TP Zone Preference"
- **Options**:
  - "Early" (uses `tp1.early`, `tp2.early`, `tp3.early`)
  - "Full" (uses `tp1.full`, `tp2.full`, `tp3.full`)
- **Default**: "Full"
- **Logic**: When TP is a zone, use selected preference

### 6. **Trade Execution Logic**
- **Risk Calculation**: Use panel's dynamic risk management (`GetDynamicRiskPercent()`)
- **Position Sizing**: Calculate based on SL distance and risk percentage
- **Entry Price**: Use current market price (Ask for LONG, Bid for SHORT)
- **Stop Loss**: 
  - Extract from webhook based on "SL Zone Preference" setting
  - Calculate SL distance in pips
  - Apply to all three partial positions (TP1, TP2, TP3)
- **Take Profits**:
  - **Override panel settings**: Use webhook TP levels instead of panel's TP1R, TP2R, TP3R
  - Calculate TP prices based on "TP Zone Preference" setting
  - If `tp3` is null in webhook, only create TP1 and TP2 positions
  - Adjust position percentages if fewer than 3 TPs (e.g., 50/50 for 2 TPs)
- **Position Labels**: "TP1Position", "TP2Position", "TP3Position" (maintain consistency)

---

## Detailed Implementation Steps

### **Phase 1: Data Models & Parsing**

#### Step 1.1: Create Webhook Data Models
- Create C# classes to deserialize webhook JSON:
  - `WebhookPayload` (top-level)
  - `PriceZone` (for entry_zone, stop_loss, tp1, tp2, tp3)
- Use `System.Text.Json` attributes for property mapping
- Handle nullable fields (`tp2`, `tp3`, `ai_decision`, `confidence`)

#### Step 1.2: Implement JSON Parsing
- Create method `ParseWebhookPayload(string json)` → `WebhookPayload`
- Add error handling for malformed JSON
- Validate required fields exist (instrument, direction, entry_zone, stop_loss)
- Log parsing errors with details

---

### **Phase 2: Configuration Parameters**

#### Step 2.1: Add Webhook Server Parameters
```csharp
[Parameter("Webhook Port", Group = "Webhook Settings", DefaultValue = 8050)]
public int WebhookPort { get; set; }

[Parameter("Webhook Enabled", Group = "Webhook Settings", DefaultValue = true)]
public bool WebhookEnabled { get; set; }
```

#### Step 2.2: Add Broker Validation Parameters
```csharp
[Parameter("Broker Match Validation", Group = "Webhook Settings", DefaultValue = true)]
public bool BrokerMatchValidation { get; set; }

[Parameter("Expected Broker Name", Group = "Webhook Settings", DefaultValue = "Pepperstone")]
public string ExpectedBrokerName { get; set; }
```

#### Step 2.3: Add Symbol Filter Parameters
```csharp
[Parameter("Symbol Filter", Group = "Webhook Settings", DefaultValue = "US30-Pepperstone")]
public string SymbolFilter { get; set; }
// Options: "US30-Pepperstone", "NAS100-Pepperstone", "XAUUSD-Pepperstone", 
// "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "NZDUSD", "USDCHF",
// "EURGBP", "EURJPY", "GBPJPY", "XAGUSD", "BTCUSD", "Accept Any"
```

#### Step 2.4: Add Zone Preference Parameters
```csharp
[Parameter("SL Zone Preference", Group = "Webhook Settings", DefaultValue = SLZonePreference.Wide)]
public SLZonePreference SLZonePreference { get; set; }

[Parameter("TP Zone Preference", Group = "Webhook Settings", DefaultValue = TPZonePreference.Full)]
public TPZonePreference TPZonePreference { get; set; }
```

#### Step 2.5: Create Enums
```csharp
public enum SLZonePreference
{
    Tight,
    Wide
}

public enum TPZonePreference
{
    Early,
    Full
}
```

---

### **Phase 3: Trade ID Tracking System**

#### Step 3.1: Add Tracking Data Structures
```csharp
private HashSet<string> _processedTradeIds = new HashSet<string>();
private Dictionary<string, List<long>> _webhookTradeToPositions = new Dictionary<string, List<long>>();
private DateTime _lastTradeIdCleanup = DateTime.UtcNow;
```

#### Step 3.2: Implement Duplicate Detection
```csharp
private bool IsTradeIdProcessed(string tradeId)
{
    return _processedTradeIds.Contains(tradeId);
}

private void MarkTradeIdProcessed(string tradeId)
{
    _processedTradeIds.Add(tradeId);
}
```

#### Step 3.3: Implement Cleanup Logic
```csharp
private void CleanupOldTradeIds()
{
    // Called daily or when day changes
    // Clear trade IDs older than 24 hours
    // For simplicity, clear all on day change
    _processedTradeIds.Clear();
    _webhookTradeToPositions.Clear();
}
```

#### Step 3.4: Link Webhook Trades to Positions
```csharp
private void LinkWebhookTradeToPositions(string tradeId, List<long> positionIds)
{
    _webhookTradeToPositions[tradeId] = positionIds;
}
```

---

### **Phase 4: Webhook Server Implementation**

#### Step 4.1: Create HTTP Listener
- Use `System.Net.HttpListener` (already used for existing server)
- Add new endpoint `/webhook` alongside existing `/newtrade`
- Listen on configured `WebhookPort` (default 8050)
- Run listener in background thread

#### Step 4.2: Implement Request Handler
```csharp
private void HandleWebhookRequest(HttpListenerContext context)
{
    // 1. Read request body
    // 2. Parse JSON to WebhookPayload
    // 3. Validate webhook
    // 4. Execute trade if valid
    // 5. Return 200 OK or error status
}
```

#### Step 4.3: Add Webhook Validation Method
```csharp
private bool ValidateWebhook(WebhookPayload webhook, out string errorMessage)
{
    // Check if webhook enabled
    // Check duplicate trade_id
    // Check broker match (if enabled)
    // Check symbol filter
    // Validate required fields
    // Return true if valid, false with error message if not
}
```

---

### **Phase 5: Symbol Mapping & Validation**

#### Step 5.1: Create Symbol Mapping Dictionary
```csharp
private Dictionary<string, string> _symbolMappings = new Dictionary<string, string>
{
    { "US30-Pepperstone", "US30" },
    { "NAS100-Pepperstone", "NAS100" },
    { "XAUUSD-Pepperstone", "XAUUSD" },
    // Add more mappings as needed
};
```

#### Step 5.2: Implement Symbol Extraction
```csharp
private string ExtractBaseSymbol(string instrument)
{
    // Extract symbol before broker suffix
    // Example: "XAUUSD-Pepperstone" → "XAUUSD"
    if (instrument.Contains("-"))
        return instrument.Split('-')[0];
    return instrument;
}

private string ExtractBrokerName(string instrument)
{
    // Extract broker after hyphen
    // Example: "XAUUSD-Pepperstone" → "Pepperstone"
    if (instrument.Contains("-"))
        return instrument.Split('-')[1];
    return null;
}
```

#### Step 5.3: Implement Symbol Mapping
```csharp
private string MapSymbolToMT(string webhookInstrument)
{
    // Try direct mapping first
    if (_symbolMappings.ContainsKey(webhookInstrument))
        return _symbolMappings[webhookInstrument];
    
    // Fall back to base symbol
    return ExtractBaseSymbol(webhookInstrument);
}
```

#### Step 5.4: Implement Symbol Filter Check
```csharp
private bool PassesSymbolFilter(string webhookInstrument)
{
    if (SymbolFilter == "Accept Any")
        return true;
    
    // Check if webhook instrument matches filter
    string baseSymbol = ExtractBaseSymbol(webhookInstrument);
    string filterBaseSymbol = ExtractBaseSymbol(SymbolFilter);
    
    return baseSymbol.Equals(filterBaseSymbol, StringComparison.OrdinalIgnoreCase);
}
```

---

### **Phase 6: Price Extraction Logic**

#### Step 6.1: Implement SL Price Extraction
```csharp
private double GetStopLossPrice(PriceZone stopLoss, string direction)
{
    if (stopLoss == null)
        return 0;
    
    if (!stopLoss.is_zone)
        return stopLoss.mid;
    
    // Use preference setting
    if (SLZonePreference == SLZonePreference.Tight)
        return stopLoss.tight;
    else
        return stopLoss.wide;
}
```

#### Step 6.2: Implement TP Price Extraction
```csharp
private double GetTakeProfitPrice(PriceZone tp, string direction)
{
    if (tp == null)
        return 0;
    
    if (!tp.is_zone)
        return tp.mid;
    
    // Use preference setting
    if (TPZonePreference == TPZonePreference.Early)
        return tp.early;
    else
        return tp.full;
}
```

#### Step 6.3: Calculate SL Distance in Pips
```csharp
private double CalculateSLDistanceInPips(double entryPrice, double slPrice, TradeType tradeType)
{
    double priceDiff = Math.Abs(entryPrice - slPrice);
    return priceDiff / Symbol.PipSize;
}
```

---

### **Phase 7: Webhook Trade Execution**

#### Step 7.1: Create Main Execution Method
```csharp
public void ExecuteWebhookTrade(WebhookPayload webhook)
{
    // 1. Validate webhook
    // 2. Determine trade direction
    // 3. Get current market price
    // 4. Extract SL and TP prices
    // 5. Calculate position sizing
    // 6. Execute partial positions
    // 7. Track trade_id
    // 8. Log execution
}
```

#### Step 7.2: Implement Trade Direction Mapping
```csharp
private TradeType GetTradeType(string direction)
{
    if (direction.Equals("LONG", StringComparison.OrdinalIgnoreCase))
        return TradeType.Buy;
    else if (direction.Equals("SHORT", StringComparison.OrdinalIgnoreCase))
        return TradeType.Sell;
    else
        throw new ArgumentException($"Invalid direction: {direction}");
}
```

#### Step 7.3: Implement Position Sizing for Webhook
```csharp
private void CalculateWebhookPositionSizes(
    double slDistancePips,
    out double pos1Volume,
    out double pos2Volume,
    out double pos3Volume,
    bool hasTP3)
{
    // Use panel's dynamic risk
    double riskPercent = GetDynamicRiskPercent();
    double riskAmount = Account.Balance * (riskPercent / 100.0);
    
    // Calculate total volume
    double totalVolume = riskAmount / (slDistancePips * Symbol.PipValue);
    totalVolume = Symbol.NormalizeVolumeInUnits(totalVolume, RoundingMode.ToNearest);
    
    if (totalVolume < Symbol.VolumeInUnitsMin)
        totalVolume = Symbol.VolumeInUnitsMin;
    
    // Split based on whether we have 3 TPs or 2 TPs
    if (hasTP3)
    {
        // Use panel's TP percentages
        pos1Volume = totalVolume * (TP1Percent / 100.0);
        pos2Volume = totalVolume * (TP2Percent / 100.0);
        pos3Volume = totalVolume * (TP3Percent / 100.0);
    }
    else
    {
        // Only 2 TPs - split 50/50 or use TP1/TP2 percentages
        double total12 = TP1Percent + TP2Percent;
        pos1Volume = totalVolume * (TP1Percent / total12);
        pos2Volume = totalVolume * (TP2Percent / total12);
        pos3Volume = 0;
    }
    
    // Normalize volumes
    pos1Volume = Symbol.NormalizeVolumeInUnits(pos1Volume, RoundingMode.ToNearest);
    pos2Volume = Symbol.NormalizeVolumeInUnits(pos2Volume, RoundingMode.ToNearest);
    if (hasTP3)
        pos3Volume = Symbol.NormalizeVolumeInUnits(pos3Volume, RoundingMode.ToNearest);
    
    // Ensure minimum volumes
    if (pos1Volume < Symbol.VolumeInUnitsMin) pos1Volume = Symbol.VolumeInUnitsMin;
    if (pos2Volume < Symbol.VolumeInUnitsMin) pos2Volume = Symbol.VolumeInUnitsMin;
    if (hasTP3 && pos3Volume < Symbol.VolumeInUnitsMin) pos3Volume = Symbol.VolumeInUnitsMin;
}
```

#### Step 7.4: Execute Webhook Positions
```csharp
private void ExecuteWebhookPositions(
    WebhookPayload webhook,
    TradeType tradeType,
    double slPrice,
    double tp1Price,
    double tp2Price,
    double tp3Price)
{
    // Get current market price
    double entryPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
    
    // Calculate SL distance
    double slDistancePips = CalculateSLDistanceInPips(entryPrice, slPrice, tradeType);
    
    // Calculate position sizes
    bool hasTP3 = (webhook.tp3 != null && tp3Price > 0);
    CalculateWebhookPositionSizes(slDistancePips, out double pos1, out double pos2, out double pos3, hasTP3);
    
    // Calculate TP distances in pips
    double tp1DistancePips = CalculateSLDistanceInPips(entryPrice, tp1Price, tradeType);
    double tp2DistancePips = CalculateSLDistanceInPips(entryPrice, tp2Price, tradeType);
    double tp3DistancePips = hasTP3 ? CalculateSLDistanceInPips(entryPrice, tp3Price, tradeType) : 0;
    
    // Execute positions
    var positionIds = new List<long>();
    
    var result1 = ExecuteMarketOrderAsync(tradeType, SymbolName, pos1, "TP1Position", slDistancePips, tp1DistancePips);
    if (result1.IsSuccessful) positionIds.Add(result1.Position.Id);
    
    var result2 = ExecuteMarketOrderAsync(tradeType, SymbolName, pos2, "TP2Position", slDistancePips, tp2DistancePips);
    if (result2.IsSuccessful) positionIds.Add(result2.Position.Id);
    
    if (hasTP3)
    {
        var result3 = ExecuteMarketOrderAsync(tradeType, SymbolName, pos3, "TP3Position", slDistancePips, tp3DistancePips);
        if (result3.IsSuccessful) positionIds.Add(result3.Position.Id);
    }
    
    // Link webhook trade_id to positions
    LinkWebhookTradeToPositions(webhook.trade_id, positionIds);
    
    // Mark trade_id as processed
    MarkTradeIdProcessed(webhook.trade_id);
    
    // Log execution
    PrintLocal($"Webhook Trade Executed: {webhook.direction} {webhook.instrument}, " +
               $"SL={slPrice:F5}, TP1={tp1Price:F5}, TP2={tp2Price:F5}" +
               (hasTP3 ? $", TP3={tp3Price:F5}" : "") +
               $", Confidence={webhook.confidence}%");
}
```

---

### **Phase 8: Integration with Existing Code**

#### Step 8.1: Modify OnStart()
- Initialize webhook server if `WebhookEnabled == true`
- Start HTTP listener on `WebhookPort`
- Initialize trade ID tracking collections
- Add webhook-specific logging

#### Step 8.2: Modify OnStop()
- Stop webhook server gracefully
- Close HTTP listener
- Save processed trade IDs (optional)

#### Step 8.3: Modify CheckDayChange()
- Add call to `CleanupOldTradeIds()` on day change
- Reset webhook-related daily counters

#### Step 8.4: Update CanTrade() Logic
- Ensure webhook trades respect same risk gates as manual trades
- Check equity thresholds before executing webhook trades

#### Step 8.5: Position Tracking Integration
- Webhook trades should be tracked by existing `_tradeManager`
- Ensure TSL (Trailing Stop Loss) applies to webhook positions
- Daily trade limits should count webhook trades

---

### **Phase 9: Error Handling & Logging**

#### Step 9.1: Webhook Validation Errors
- Log rejected webhooks with reason:
  - Duplicate trade_id
  - Broker mismatch
  - Symbol filter mismatch
  - Missing required fields
  - Invalid JSON format

#### Step 9.2: Execution Errors
- Log failed position executions
- Handle partial failures (e.g., TP1 succeeds but TP2 fails)
- Notify if risk gates prevent execution

#### Step 9.3: Server Errors
- Log HTTP listener errors
- Handle port conflicts
- Graceful degradation if webhook server fails to start

#### Step 9.4: Enhanced Logging
```csharp
private void LogWebhookEvent(string eventType, string message, WebhookPayload webhook = null)
{
    string logMsg = $"[WEBHOOK-{eventType}] {message}";
    if (webhook != null)
        logMsg += $" | TradeID={webhook.trade_id}, Instrument={webhook.instrument}";
    PrintLocal(logMsg);
}
```

---

### **Phase 10: Testing & Validation**

#### Step 10.1: Unit Testing Scenarios
1. **Valid webhook with 3 TPs** - should execute all positions
2. **Valid webhook with 2 TPs** - should execute only TP1 and TP2
3. **Duplicate trade_id** - should reject second attempt
4. **Broker mismatch with validation ON** - should reject
5. **Broker mismatch with validation OFF** - should accept
6. **Symbol filter match** - should accept
7. **Symbol filter mismatch** - should reject
8. **"Accept Any" symbol filter** - should accept all symbols
9. **Zone vs single price** - should use correct helper fields
10. **SL/TP zone preferences** - should respect settings

#### Step 10.2: Integration Testing
1. Test with existing manual trading panel
2. Verify webhook trades respect risk management
3. Confirm TSL applies to webhook positions
4. Validate daily limits count webhook trades
5. Test multiple concurrent webhook trades

#### Step 10.3: Edge Cases
1. Null TP2 and TP3 fields
2. Malformed JSON
3. Missing required fields
4. Invalid direction values
5. Zero or negative prices
6. Extremely tight SL (< 1 pip)

---

## Configuration Summary

### New Parameters Added

| Parameter | Group | Type | Default | Description |
|-----------|-------|------|---------|-------------|
| Webhook Port | Webhook Settings | int | 8050 | Port to receive webhooks |
| Webhook Enabled | Webhook Settings | bool | true | Enable/disable webhook server |
| Broker Match Validation | Webhook Settings | bool | true | Validate broker name matches |
| Expected Broker Name | Webhook Settings | string | "Pepperstone" | Expected broker suffix |
| Symbol Filter | Webhook Settings | string | "US30-Pepperstone" | Symbol to accept or "Accept Any" |
| SL Zone Preference | Webhook Settings | enum | Wide | Tight or Wide SL |
| TP Zone Preference | Webhook Settings | enum | Full | Early or Full TP |

---

## Data Flow Diagram

```
Webhook POST → HTTP Listener (Port 8050)
                    ↓
            Parse JSON Payload
                    ↓
            Validate Webhook
            ├─ Check Enabled
            ├─ Check Duplicate trade_id
            ├─ Check Broker Match (if enabled)
            ├─ Check Symbol Filter
            └─ Validate Required Fields
                    ↓
            [If Valid] Extract Prices
            ├─ Get SL Price (based on preference)
            ├─ Get TP1 Price (based on preference)
            ├─ Get TP2 Price (based on preference)
            └─ Get TP3 Price (if exists)
                    ↓
            Calculate Position Sizing
            ├─ Use Dynamic Risk %
            ├─ Calculate SL Distance in Pips
            └─ Split into TP1/TP2/TP3 volumes
                    ↓
            Execute Market Orders
            ├─ TP1Position (with SL & TP1)
            ├─ TP2Position (with SL & TP2)
            └─ TP3Position (with SL & TP3, if exists)
                    ↓
            Track & Log
            ├─ Add trade_id to processed set
            ├─ Link trade_id to position IDs
            └─ Log execution details
                    ↓
            Return 200 OK
```

---

## Risk Management Integration

### Webhook Trades Use Panel Settings For:
1. **Risk Percentage** - Dynamic risk calculation based on account performance
2. **Position Sizing** - Volume calculated from risk % and SL distance
3. **TP Split Percentages** - TP1%, TP2%, TP3% from panel parameters
4. **Daily Limits** - Max positive/negative trades per day
5. **Equity Gates** - Max loss % and daily stop loss %
6. **Trailing Stop Loss** - TSL settings apply to webhook positions

### Webhook Overrides Panel Settings For:
1. **Stop Loss Price** - Uses webhook SL (not panel's default SL pips)
2. **Take Profit Prices** - Uses webhook TP1/TP2/TP3 (not panel's TP1R/TP2R/TP3R)
3. **Trade Direction** - Uses webhook direction
4. **Entry Timing** - Executes immediately on webhook receipt

---

## Symbol Mapping Reference

### Predefined Symbols for Filter Dropdown
1. US30-Pepperstone
2. NAS100-Pepperstone
3. XAUUSD-Pepperstone
4. XAGUSD-Pepperstone
5. EURUSD
6. GBPUSD
7. USDJPY
8. AUDUSD
9. USDCAD
10. NZDUSD
11. USDCHF
12. EURGBP
13. EURJPY
14. GBPJPY
15. BTCUSD
16. **Accept Any** (special option)

### Broker Suffix Handling
- Extract broker name after hyphen: "XAUUSD-Pepperstone" → "Pepperstone"
- If no hyphen, assume no broker suffix
- Compare extracted broker with `ExpectedBrokerName` parameter

---

## Files to Modify

### Primary File
- **`SkyAnalyst Automated Trader.cs`** - Main implementation file

### Supporting Files (if needed)
- Create `WebhookModels.cs` - Data models for webhook payload (optional, can be in main file)

---

## Implementation Checklist

- [ ] **Phase 1**: Create webhook data models and JSON parsing
- [ ] **Phase 2**: Add all configuration parameters and enums
- [ ] **Phase 3**: Implement trade ID tracking system
- [ ] **Phase 4**: Create HTTP listener and webhook endpoint
- [ ] **Phase 5**: Implement symbol mapping and validation
- [ ] **Phase 6**: Create price extraction logic with zone preferences
- [ ] **Phase 7**: Implement webhook trade execution
- [ ] **Phase 8**: Integrate with existing bot lifecycle (OnStart, OnStop, etc.)
- [ ] **Phase 9**: Add comprehensive error handling and logging
- [ ] **Phase 10**: Test all scenarios and edge cases

---

## Notes & Considerations

### Multi-Trade Support
- Bot can handle multiple active webhook trades simultaneously
- Each trade tracked by unique `trade_id`
- Position IDs linked to originating webhook trade
- Existing `_tradeManager` handles all positions uniformly

### Broker Price Feed Validation
- Critical for US indexes where broker prices vary significantly
- Example: US30 on Pepperstone vs FXCM can differ by several points
- Setting defaults to ON for safety
- Can be disabled for symbols where broker doesn't matter

### Zone Preferences
- "Wide" SL gives more breathing room (default for safety)
- "Tight" SL reduces risk but increases stop-out probability
- "Full" TP targets maximum profit (default for best R:R)
- "Early" TP takes profit sooner (more conservative)

### Performance Considerations
- HTTP listener runs on separate thread
- Webhook processing should be fast (<100ms)
- Must respond within 5 seconds per webhook spec
- Trade ID cleanup on day change prevents memory growth

### Security Considerations
- Webhook server listens on localhost only (127.0.0.1)
- No authentication required (local-only access)
- Consider adding API key validation if exposing externally
- Validate all input data before processing

---

## Future Enhancements (Out of Scope)

1. **Webhook Authentication** - API key validation
2. **Webhook History** - Store all received webhooks in database
3. **Position Management via Webhook** - Close/modify existing positions
4. **Multiple Webhook Sources** - Different ports for different signal providers
5. **Conditional Execution** - Only trade during specific hours/sessions
6. **Confidence Threshold** - Only execute if AI confidence > X%
7. **Risk Scaling by Confidence** - Adjust position size based on confidence score

---

## Conclusion

This implementation plan provides a comprehensive roadmap for integrating webhook-based automated trading into the SkyAnalyst Automated Trader. The design maintains compatibility with existing manual trading functionality while adding robust automated execution capabilities. The modular approach allows for incremental implementation and testing of each phase.

**Key Success Factors:**
- Proper validation prevents duplicate trades and mismatched symbols
- Zone preferences give traders control over execution style
- Integration with existing risk management ensures safety
- Comprehensive logging enables troubleshooting and auditing
- Flexible symbol filtering supports multiple trading instruments

**Estimated Implementation Time:** 8-12 hours for experienced C#/cAlgo developer

---

*Document Version: 1.0*  
*Created: December 24, 2025*  
*Author: SkyAnalyst AI Development Team*
