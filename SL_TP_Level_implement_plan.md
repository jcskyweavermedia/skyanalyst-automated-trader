
# 📋 **FINAL PLAN: TP and SL Broadcasting with Reverse Mode Support**

## **Overview**
Enable the Server to broadcast TP and SL level modifications to the Receiver in real-time. The Server will send both price and pip difference formats, while the Receiver applies them correctly based on its settings, including reversing them when in Reverse trade mode.

---

## **PHASE 1: Add Modification Mode Infrastructure**

### **1.1 Add TPModificationMode Enum to Both Files**
```csharp
public enum TPModificationMode
{
    Pip_Diff,
    Price
}
```

### **1.2 Add SLModificationMode Enum to Both Files**
```csharp
public enum SLModificationMode
{
    Pip_Diff,
    Price
}
```

### **1.3 Add Parameters to Receiver**
```csharp
[Parameter("TP Modification Mode", Group = "Receiver", DefaultValue = TPModificationMode.Price)]
public TPModificationMode TPModMode { get; set; }

[Parameter("SL Modification Mode", Group = "Receiver", DefaultValue = SLModificationMode.Price)]
public SLModificationMode SLModMode { get; set; }
```

---

## **PHASE 2: Broadcast Initial Trade with TP Levels (Server)**

### **2.1 Update SendJsonMessage Method**
**IMPORTANT**: Send TP levels for all 3 positions in the initial trade broadcast.

```csharp
private void SendJsonMessage(TradeType tradeType, double slPips, double slPrice)
{
    // Calculate current price for TP calculation
    double currentPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
    
    var msg = new
    {
        action = tradeType == TradeType.Buy ? "Buy" : "Sell",
        symbol = SymbolName,
        sl_pips = slPips,
        sl_price = slPrice,
        current_price = currentPrice,
        tp_levels = new[]
        {
            new { 
                label = "TP1Position",
                r_value = TP1R, 
                tp_pips = slPips * TP1R,
                tp_price = CalculateTPPrice(tradeType, currentPrice, slPips * TP1R)
            },
            new { 
                label = "TP2Position",
                r_value = TP2R, 
                tp_pips = slPips * TP2R,
                tp_price = CalculateTPPrice(tradeType, currentPrice, slPips * TP2R)
            },
            new { 
                label = "TP3Position",
                r_value = TP3R, 
                tp_pips = slPips * TP3R,
                tp_price = CalculateTPPrice(tradeType, currentPrice, slPips * TP3R)
            }
        }
    };
    
    string jsonMsg = JsonSerializer.Serialize(msg);
    
    // Broadcast to all active ports
    for (int i = 1; i <= PortsActive; i++)
    {
        int port = GetPortNumber(i);
        Task.Run(() => SendToPort(jsonMsg, port));
    }
    
    PrintLocal($"Broadcasted Trade: {tradeType}, SL={slPips:F1} pips, TP1={TP1R}R, TP2={TP2R}R, TP3={TP3R}R");
}

private double CalculateTPPrice(TradeType tradeType, double entryPrice, double tpPips)
{
    double tpDistance = tpPips * Symbol.PipSize;
    
    if (tradeType == TradeType.Buy)
        return entryPrice + tpDistance;
    else
        return entryPrice - tpDistance;
}
```

---

## **PHASE 3: Receive and Apply TP Levels (Receiver)**

### **3.1 Update ExecuteReceivedTrade Signature**
```csharp
private void ExecuteReceivedTrade(TradeType receivedType, double? slPips, double? slPrice, JsonElement? tpLevelsElement)
```

### **3.2 Update ProcessIncomingTrade to Pass TP Levels**
```csharp
private void ProcessIncomingTrade(string json)
{
    try
    {
        var doc = JsonDocument.Parse(json);
        string action = doc.RootElement.GetProperty("action").GetString();
        
        if (action == "ModifyTP")
        {
            ProcessTPModification(doc.RootElement);
            return;
        }
        
        if (action == "ModifySL")
        {
            ProcessSLModification(doc.RootElement);
            return;
        }
        
        if (action == "Close All Positions")
        {
            PrintLocal("Received: Close All Positions");
            CloseAllPositions();
            return;
        }

        TradeType receivedType = action == "Buy" ? TradeType.Buy : TradeType.Sell;
        PrintLocal($"Received: {action} for {SymbolName}");

        double? slPips = null;
        double? slPrice = null;
        JsonElement? tpLevels = null;

        if (doc.RootElement.TryGetProperty("sl_pips", out var slPipsEl))
            slPips = slPipsEl.GetDouble();

        if (doc.RootElement.TryGetProperty("sl_price", out var slPriceEl))
            slPrice = slPriceEl.GetDouble();

        if (doc.RootElement.TryGetProperty("tp_levels", out var tpLevelsEl))
            tpLevels = tpLevelsEl;

        ExecuteReceivedTrade(receivedType, slPips, slPrice, tpLevels);
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing trade: {ex.Message}");
    }
}
```

### **3.3 Add TP Application Logic to ExecuteReceivedTrade**
```csharp
// 7. Calculate TP levels for receiver (reverse if needed)
double finalTP1 = 0, finalTP2 = 0, finalTP3 = 0;

if (hasTPLevels)
{
    if (TPModMode == TPModificationMode.Price)
    {
        // Use price mode - reverse if in Reverse mode
        if (TradeModeParam == TradeMode.Reverse)
        {
            finalTP1 = ReverseTPPrice(tp1Price, receivedType, actualType);
            finalTP2 = ReverseTPPrice(tp2Price, receivedType, actualType);
            finalTP3 = ReverseTPPrice(tp3Price, receivedType, actualType);
            PrintLocal($"TP Prices reversed: TP1={finalTP1:F5}, TP2={finalTP2:F5}, TP3={finalTP3:F5}");
        }
        else
        {
            finalTP1 = tp1Price;
            finalTP2 = tp2Price;
            finalTP3 = tp3Price;
            PrintLocal($"TP Prices copied: TP1={finalTP1:F5}, TP2={finalTP2:F5}, TP3={finalTP3:F5}");
        }
    }
    else // Pip_Diff mode
    {
        double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
        finalTP1 = CalculateTPFromPips(actualType, currentPrice, tp1Pips);
        finalTP2 = CalculateTPFromPips(actualType, currentPrice, tp2Pips);
        finalTP3 = CalculateTPFromPips(actualType, currentPrice, tp3Pips);
        PrintLocal($"TP from pips: TP1={finalTP1:F5}, TP2={finalTP2:F5}, TP3={finalTP3:F5}");
    }
}
else
{
    // No TP levels received - use receiver's own R values
    double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
    finalTP1 = CalculateTPFromPips(actualType, currentPrice, finalSlPips * TP1R);
    finalTP2 = CalculateTPFromPips(actualType, currentPrice, finalSlPips * TP2R);
    finalTP3 = CalculateTPFromPips(actualType, currentPrice, finalSlPips * TP3R);
    PrintLocal($"Using receiver's TP settings: TP1={TP1R}R, TP2={TP2R}R, TP3={TP3R}R");
}

// 8. Execute the 3 positions with TP levels
ExecuteMarketOrder(actualType, pos1, "TP1Position", finalSlPips, finalTP1);
ExecuteMarketOrder(actualType, pos2, "TP2Position", finalSlPips, finalTP2);
ExecuteMarketOrder(actualType, pos3, "TP3Position", finalSlPips, finalTP3);
```

### **3.4 Add Helper Methods**
```csharp
private double ReverseTPPrice(double originalTPPrice, TradeType originalType, TradeType reversedType)
{
    double currentBid = Symbol.Bid;
    double currentAsk = Symbol.Ask;
    double originalEntry = (originalType == TradeType.Buy) ? currentAsk : currentBid;
    double tpDistance = Math.Abs(originalTPPrice - originalEntry);
    double reversedEntry = (reversedType == TradeType.Buy) ? currentAsk : currentBid;
    
    if (reversedType == TradeType.Buy)
        return reversedEntry + tpDistance;
    else
        return reversedEntry - tpDistance;
}

private double CalculateTPFromPips(TradeType tradeType, double entryPrice, double tpPips)
{
    double tpDistance = tpPips * Symbol.PipSize;
    
    if (tradeType == TradeType.Buy)
        return entryPrice + tpDistance;
    else
        return entryPrice - tpDistance;
}

private void ExecuteMarketOrder(TradeType tradeType, double volume, string label, double slPips, double tpPrice)
{
    double slDistance = slPips * Symbol.PipSize;
    
#pragma warning disable 0618
    var result = ExecuteMarketOrder(tradeType, SymbolName, volume, label, slDistance, tpPrice);
#pragma warning restore 0618
    
    if (result.IsSuccessful)
    {
        PrintLocal($"Position opened: {label}, Volume={volume}, SL={slPips:F1}p, TP={tpPrice:F5}");
    }
    else
    {
        PrintLocal($"Failed to open {label}: {result.Error}");
    }
}
```

---

## **PHASE 4: Add TP and SL Modification Detection (Server)**

### **4.1 Track Position TP and SL Levels**
Add to Server's main class:

```csharp
private Dictionary<long, double?> _positionTPLevels = new Dictionary<long, double?>();
private Dictionary<long, double?> _positionSLLevels = new Dictionary<long, double?>();
```

### **4.2 Add Modification Monitoring in OnTick**
```csharp
protected override void OnTick()
{
    CheckDayChange();
    CheckHardStops();
    _tradeManager.CheckPositions();
    CheckTPModifications();
    CheckSLModifications();
}

private void CheckTPModifications()
{
    foreach (var pos in Positions.Where(p => p.SymbolName == SymbolName))
    {
        if (!_positionTPLevels.ContainsKey(pos.Id))
        {
            _positionTPLevels[pos.Id] = pos.TakeProfit;
        }
        else
        {
            double? oldTP = _positionTPLevels[pos.Id];
            double? newTP = pos.TakeProfit;
            
            if (oldTP != newTP)
            {
                PrintLocal($"TP Modified: Position {pos.Id} ({pos.Label}), Old: {oldTP}, New: {newTP}");
                BroadcastTPModification(pos);
                _positionTPLevels[pos.Id] = newTP;
            }
        }
    }
    
    var closedIds = _positionTPLevels.Keys.Where(id => !Positions.Any(p => p.Id == id)).ToList();
    foreach (var id in closedIds)
        _positionTPLevels.Remove(id);
}

private void CheckSLModifications()
{
    foreach (var pos in Positions.Where(p => p.SymbolName == SymbolName))
    {
        if (!_positionSLLevels.ContainsKey(pos.Id))
        {
            _positionSLLevels[pos.Id] = pos.StopLoss;
        }
        else
        {
            double? oldSL = _positionSLLevels[pos.Id];
            double? newSL = pos.StopLoss;
            
            if (oldSL != newSL)
            {
                PrintLocal($"SL Modified: Position {pos.Id} ({pos.Label}), Old: {oldSL}, New: {newSL}");
                BroadcastSLModification(pos);
                _positionSLLevels[pos.Id] = newSL;
            }
        }
    }
    
    var closedIds = _positionSLLevels.Keys.Where(id => !Positions.Any(p => p.Id == id)).ToList();
    foreach (var id in closedIds)
        _positionSLLevels.Remove(id);
}
```

---

## **PHASE 5: Broadcast TP and SL Modifications (Server)**

### **5.1 Add BroadcastTPModification Method**
```csharp
private void BroadcastTPModification(Position pos)
{
    if (!pos.TakeProfit.HasValue)
    {
        PrintLocal($"TP removed for position {pos.Id} - not broadcasting");
        return;
    }
    
    double currentPrice = (pos.TradeType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
    double tpPrice = pos.TakeProfit.Value;
    double tpPipDiff = Math.Abs(currentPrice - tpPrice) / Symbol.PipSize;
    
    var msg = new
    {
        action = "ModifyTP",
        symbol = SymbolName,
        position_label = pos.Label,
        trade_type = pos.TradeType.ToString(),
        tp_price = tpPrice,
        tp_pip_diff = tpPipDiff,
        current_price = currentPrice,
        entry_price = pos.EntryPrice
    };
    
    string jsonMsg = JsonSerializer.Serialize(msg);
    
    for (int i = 1; i <= PortsActive; i++)
    {
        int port = GetPortNumber(i);
        Task.Run(() => SendToPort(jsonMsg, port));
    }
    
    PrintLocal($"Broadcasted TP Mod: {pos.Label}, Price={tpPrice:F5}, PipDiff={tpPipDiff:F1}");
}
```

### **5.2 Add BroadcastSLModification Method**
```csharp
private void BroadcastSLModification(Position pos)
{
    if (!pos.StopLoss.HasValue)
    {
        PrintLocal($"SL removed for position {pos.Id} - not broadcasting");
        return;
    }
    
    double currentPrice = (pos.TradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
    double slPrice = pos.StopLoss.Value;
    double slPipDiff = Math.Abs(currentPrice - slPrice) / Symbol.PipSize;
    
    var msg = new
    {
        action = "ModifySL",
        symbol = SymbolName,
        position_label = pos.Label,
        trade_type = pos.TradeType.ToString(),
        sl_price = slPrice,
        sl_pip_diff = slPipDiff,
        current_price = currentPrice,
        entry_price = pos.EntryPrice
    };
    
    string jsonMsg = JsonSerializer.Serialize(msg);
    
    for (int i = 1; i <= PortsActive; i++)
    {
        int port = GetPortNumber(i);
        Task.Run(() => SendToPort(jsonMsg, port));
    }
    
    PrintLocal($"Broadcasted SL Mod: {pos.Label}, Price={slPrice:F5}, PipDiff={slPipDiff:F1}");
}
```

---

## **PHASE 6: Process TP Modification with Reverse Mode (Receiver)**

### **6.1 Add ProcessTPModification Method**
```csharp
private void ProcessTPModification(JsonElement data)
{
    try
    {
        string posLabel = data.GetProperty("position_label").GetString();
        string tradeTypeStr = data.GetProperty("trade_type").GetString();
        double tpPrice = data.GetProperty("tp_price").GetDouble();
        double tpPipDiff = data.GetProperty("tp_pip_diff").GetDouble();
        
        PrintLocal($"Received TP Mod: {posLabel}, Price={tpPrice:F5}, PipDiff={tpPipDiff:F1}");
        
        var matchingPositions = Positions.Where(p => 
            p.SymbolName == SymbolName && 
            p.Label == posLabel).ToList();
        
        if (matchingPositions.Count == 0)
        {
            PrintLocal($"No matching position for: {posLabel}");
            return;
        }
        
        double newTP = 0;
        TradeType originalType = Enum.Parse<TradeType>(tradeTypeStr);
        TradeType actualType = (TradeModeParam == TradeMode.Copy) 
            ? originalType 
            : (originalType == TradeType.Buy ? TradeType.Sell : TradeType.Buy);
        
        if (TPModMode == TPModificationMode.Price)
        {
            if (TradeModeParam == TradeMode.Reverse)
            {
                newTP = ReverseTPPrice(tpPrice, originalType, actualType);
                PrintLocal($"TP Price (reversed): {newTP:F5}");
            }
            else
            {
                newTP = tpPrice;
                PrintLocal($"TP Price: {newTP:F5}");
            }
        }
        else
        {
            double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
            newTP = CalculateTPFromPips(actualType, myCurrentPrice, tpPipDiff);
            PrintLocal($"TP from pips: {tpPipDiff:F1}p = {newTP:F5}");
        }
        
        foreach (var pos in matchingPositions)
        {
            ModifyPositionTP(pos, newTP);
        }
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing TP mod: {ex.Message}");
    }
}

private void ModifyPositionTP(Position pos, double newTP)
{
    try
    {
#pragma warning disable 0618
        var result = ModifyPosition(pos, pos.StopLoss, newTP);
#pragma warning restore 0618
        
        if (result.IsSuccessful)
            PrintLocal($"TP Modified: {pos.Id} ({pos.Label}), New TP: {newTP:F5}");
        else
            PrintLocal($"TP Mod Failed: {pos.Id}, Error: {result.Error}");
    }
    catch (Exception ex)
    {
        PrintLocal($"Exception modifying TP: {ex.Message}");
    }
}
```

---

## **PHASE 7: Process SL Modification with Reverse Mode (Receiver)**

### **7.1 Add ProcessSLModification Method**
```csharp
private void ProcessSLModification(JsonElement data)
{
    try
    {
        string posLabel = data.GetProperty("position_label").GetString();
        string tradeTypeStr = data.GetProperty("trade_type").GetString();
        double slPrice = data.GetProperty("sl_price").GetDouble();
        double slPipDiff = data.GetProperty("sl_pip_diff").GetDouble();
        
        PrintLocal($"Received SL Mod: {posLabel}, Price={slPrice:F5}, PipDiff={slPipDiff:F1}");
        
        var matchingPositions = Positions.Where(p => 
            p.SymbolName == SymbolName && 
            p.Label == posLabel).ToList();
        
        if (matchingPositions.Count == 0)
        {
            PrintLocal($"No matching position for: {posLabel}");
            return;
        }
        
        double newSL = 0;
        TradeType originalType = Enum.Parse<TradeType>(tradeTypeStr);
        TradeType actualType = (TradeModeParam == TradeMode.Copy) 
            ? originalType 
            : (originalType == TradeType.Buy ? TradeType.Sell : TradeType.Buy);
        
        if (SLModMode == SLModificationMode.Price)
        {
            if (TradeModeParam == TradeMode.Reverse)
            {
                newSL = ReverseSLPrice(slPrice, originalType, actualType);
                PrintLocal($"SL Price (reversed): {newSL:F5}");
            }
            else
            {
                newSL = slPrice;
                PrintLocal($"SL Price: {newSL:F5}");
            }
        }
        else
        {
            double myCurrentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
            newSL = CalculateSLFromPips(actualType, myCurrentPrice, slPipDiff);
            PrintLocal($"SL from pips: {slPipDiff:F1}p = {newSL:F5}");
        }
        
        foreach (var pos in matchingPositions)
        {
            ModifyPositionSL(pos, newSL);
        }
    }
    catch (Exception ex)
    {
        PrintLocal($"Error processing SL mod: {ex.Message}");
    }
}

private double ReverseSLPrice(double originalSLPrice, TradeType originalType, TradeType reversedType)
{
    double currentBid = Symbol.Bid;
    double currentAsk = Symbol.Ask;
    double originalEntry = (originalType == TradeType.Buy) ? currentAsk : currentBid;
    double slDistance = Math.Abs(originalSLPrice - originalEntry);
    double reversedEntry = (reversedType == TradeType.Buy) ? currentAsk : currentBid;
    
    if (reversedType == TradeType.Buy)
        return reversedEntry - slDistance;
    else
        return reversedEntry + slDistance;
}

private double CalculateSLFromPips(TradeType tradeType, double entryPrice, double slPips)
{
    double slDistance = slPips * Symbol.PipSize;
    
    if (tradeType == TradeType.Buy)
        return entryPrice - slDistance;
    else
        return entryPrice + slDistance;
}

private void ModifyPositionSL(Position pos, double newSL)
{
    try
    {
#pragma warning disable 0618
        var result = ModifyPosition(pos, newSL, pos.TakeProfit);
#pragma warning restore 0618
        
        if (result.IsSuccessful)
            PrintLocal($"SL Modified: {pos.Id} ({pos.Label}), New SL: {newSL:F5}");
        else
            PrintLocal($"SL Mod Failed: {pos.Id}, Error: {result.Error}");
    }
    catch (Exception ex)
    {
        PrintLocal($"Exception modifying SL: {ex.Message}");
    }
}
```

---

## **PHASE 8: Handle Edge Cases & Cleanup**

### **8.1 Server: Position Cleanup**
```csharp
private void OnPositionClosed(PositionClosedEventArgs args)
{
    // ... existing logic ...
    
    if (_positionTPLevels.ContainsKey(args.Position.Id))
        _positionTPLevels.Remove(args.Position.Id);
    
    if (_positionSLLevels.ContainsKey(args.Position.Id))
        _positionSLLevels.Remove(args.Position.Id);
}
```

---

## **Implementation Summary**

### **Files to Modify:**
1. **Server (Trading Panel 2.0 Server.cs)**
   - Add `TPModificationMode` and `SLModificationMode` enums
   - Add `_positionTPLevels` and `_positionSLLevels` dictionaries
   - Update [SendJsonMessage](cci:1://file:///d:/iCloud%20Drive/iCloudDrive/Skyweaver%20Trading/Bot%20Codes/Trading%20Panel%20New%20Reversal/Trading%20Panel%202.0%20Server.cs:314:8-354:9) with TP info
   - Add `CheckTPModifications` and `CheckSLModifications` methods
   - Add `BroadcastTPModification` and `BroadcastSLModification` methods
   - Add `CalculateTPPrice` helper
   - Update [OnPositionClosed](cci:1://file:///d:/iCloud%20Drive/iCloudDrive/Skyweaver%20Trading/Bot%20Codes/Trading%20Panel%20New%20Reversal/Trading%20Panel%202.0%20Server.cs:431:8-472:9) cleanup

2. **Receiver (Trading Panel 2.0 Receiver.cs)**
   - Add `TPModificationMode` and `SLModificationMode` enums
   - Add `TPModMode` and `SLModMode` parameters
   - Update [ProcessIncomingTrade](cci:1://file:///d:/iCloud%20Drive/iCloudDrive/Skyweaver%20Trading/Bot%20Codes/Trading%20Panel%20New%20Reversal/Trading%20Panel%202.0%20Receiver.cs:356:8-414:9) to handle ModifyTP and ModifySL
   - Add `ProcessTPModification` and `ProcessSLModification` methods
   - Add helper methods: `ReverseTPPrice`, `ReverseSLPrice`, `CalculateTPFromPips`, `CalculateSLFromPips`
   - Add `ModifyPositionTP` and `ModifyPositionSL` methods
   - Update [ExecuteReceivedTrade](cci:1://file:///c:/Users/juanc/Downloads/Trading%20Panel%202.0%20Receiver.cs:391:8-493:9) to apply TP levels

### **Key Features:**
- ✅ **Initial trade broadcasts TP levels for all 3 positions**
- ✅ **Receiver applies broadcasted TP levels to its 3 positions**
- ✅ **TP and SL levels are reversed when in Reverse trade mode**
- ✅ **Real-time TP modification broadcasting**
- ✅ **Real-time SL modification broadcasting**
- ✅ **Both price and pip difference sent in JSON**
- ✅ **Receiver chooses format based on TPModMode/SLModMode settings**
- ✅ **Manual trading panel uses Receiver's own TP settings**
- ✅ **Copy/Reverse mode fully supported**

### **JSON Message Formats:**

**Initial Trade:**
```json
{
  "action": "Buy",
  "symbol": "US30",
  "sl_pips": 100,
  "sl_price": 42500.0,
  "current_price": 42600.0,
  "tp_levels": [
    {"label": "TP1Position", "r_value": 1.0, "tp_pips": 100, "tp_price": 42700.0},
    {"label": "TP2Position", "r_value": 2.0, "tp_pips": 200, "tp_price": 42800.0},
    {"label": "TP3Position", "r_value": 10.0, "tp_pips": 1000, "tp_price": 43600.0}
  ]
}
```

**TP Modification:**
```json
{
  "action": "ModifyTP",
  "symbol": "US30",
  "position_label": "TP1Position",
  "trade_type": "Buy",
  "tp_price": 42750.0,
  "tp_pip_diff": 150.0,
  "current_price": 42600.0,
  "entry_price": 42600.0
}
```

**SL Modification:**
```json
{
  "action": "ModifySL",
  "symbol": "US30",
  "position_label": "TP1Position",
  "trade_type": "Buy",
  "sl_price": 42450.0,
  "sl_pip_diff": 150.0,
  "current_price": 42600.0,
  "entry_price": 42600.0
}
```

---

**FINAL PLAN COMPLETE - Ready to implement when you approve!**