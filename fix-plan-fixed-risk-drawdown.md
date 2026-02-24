# Fix Plan: Fixed Risk Mode Drawdown Calculation

## Problem Summary

When using **Fixed Risk mode**, the bot incorrectly uses `_effectiveStartingBalance` (from Dynamic Risk Settings parameter) for max drawdown calculations instead of using the actual account balance when the bot started.

### Current Behavior (Incorrect)
- Fixed Risk mode uses the "Starting Balance" parameter (intended for Dynamic Risk scaling)
- Max Drawdown is calculated against this configured value, not the actual starting balance
- This causes incorrect drawdown gates in Fixed Risk mode

### Expected Behavior (Correct)
- **Fixed Risk mode**: Max Drawdown should be calculated against the actual account balance when bot started
- **Dynamic Risk mode**: Max Drawdown should be calculated against the configured Starting Balance parameter (for scaling purposes)
- Daily Loss calculation is already correct (uses `_dailyStartBalance`)

---

## Root Cause Analysis

### File: `SkyAnalyst Automated Trader.cs`

**1. Initialization (Lines 643-652)**
```csharp
if (StartingBalance <= 0)
{
    _effectiveStartingBalance = Account.Balance;
}
else
{
    _effectiveStartingBalance = StartingBalance;
}
```
- Sets `_effectiveStartingBalance` from the "Starting Balance" parameter
- This value is used for ALL drawdown calculations regardless of risk mode

**2. CanTrade() Method (Lines 1173-1178)**
```csharp
double maxDrawdownThreshold = _effectiveStartingBalance * (1.0 - (MaxDrawdown / 100.0));
if (eq <= maxDrawdownThreshold)
{
    PrintLocal($"Max Drawdown hit: Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2} (-{MaxDrawdown}%)");
    return false;
}
```
- Uses `_effectiveStartingBalance` for max drawdown check
- No differentiation between Fixed and Dynamic risk modes

**3. CheckHardStops() Method (Lines 1518-1525)**
```csharp
double maxDrawdownThreshold = _effectiveStartingBalance * (1.0 - (MaxDrawdown / 100.0));
if (eq <= maxDrawdownThreshold)
{
    CloseAllPositions();
    _stopTrading = true;
    PrintLocal($"HARD STOP: Max Drawdown -{MaxDrawdown}% hit | Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2}");
    return;
}
```
- Same issue - uses `_effectiveStartingBalance` regardless of risk mode

---

## Solution Design

### Approach
Introduce a new private field to track the **actual starting balance** for Fixed Risk mode, separate from the Dynamic Risk scaling balance.

### Implementation Steps

#### Step 1: Add New Private Field
Add a new field to store the actual account balance at bot start:
```csharp
private double _actualStartingBalance;  // For Fixed Risk drawdown calculations
private double _effectiveStartingBalance;  // For Dynamic Risk scaling only
```

#### Step 2: Update OnStart() Initialization
Modify the initialization logic to set both values appropriately:

```csharp
// Always capture actual starting balance (for Fixed Risk mode)
_actualStartingBalance = Account.Balance;

// Set effective starting balance for Dynamic Risk scaling
if (StartingBalance <= 0)
{
    _effectiveStartingBalance = Account.Balance;
    PrintLocal($"Starting Balance auto-detected: ${_effectiveStartingBalance:F2}");
}
else
{
    _effectiveStartingBalance = StartingBalance;
    PrintLocal($"Using configured Starting Balance: ${_effectiveStartingBalance:F2}");
}

// Log the balance being used for drawdown calculations
if (RiskMode == RiskModeType.Fixed)
{
    PrintLocal($"Max Drawdown will be calculated from actual starting balance: ${_actualStartingBalance:F2}");
}
else
{
    PrintLocal($"Max Drawdown will be calculated from configured starting balance: ${_effectiveStartingBalance:F2}");
}
```

#### Step 3: Create Helper Method
Add a method to get the correct balance for drawdown calculations:

```csharp
private double GetDrawdownBaseBalance()
{
    // Fixed Risk: Use actual starting balance
    // Dynamic Risk: Use configured starting balance (for scaling consistency)
    return (RiskMode == RiskModeType.Fixed) ? _actualStartingBalance : _effectiveStartingBalance;
}
```

#### Step 4: Update CanTrade() Method
Replace the hardcoded `_effectiveStartingBalance` with the helper method:

```csharp
public bool CanTrade()
{
    if (_stopTrading) return false;

    double eq = Account.Equity;
    
    // Max drawdown check - use appropriate base balance
    double baseBalance = GetDrawdownBaseBalance();
    double maxDrawdownThreshold = baseBalance * (1.0 - (MaxDrawdown / 100.0));
    if (eq <= maxDrawdownThreshold)
    {
        PrintLocal($"Max Drawdown hit: Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2} (-{MaxDrawdown}%) from base ${baseBalance:F2}");
        return false;
    }

    // Daily loss check (already correct - uses _dailyStartBalance)
    double dailyLoss = _dailyStartBalance - eq;
    double dailyLossPercent = (dailyLoss / _dailyStartBalance) * 100.0;
    if (dailyLossPercent >= MaxDailyLoss)
    {
        PrintLocal($"Daily Loss limit hit: {dailyLossPercent:F2}% >= {MaxDailyLoss:F2}%");
        return false;
    }

    return true;
}
```

#### Step 5: Update CheckHardStops() Method
Apply the same fix to the hard stop check:

```csharp
private void CheckHardStops()
{
    if (_stopTrading) return;

    double eq = Account.Equity;
    
    // Max drawdown hard stop - use appropriate base balance
    double baseBalance = GetDrawdownBaseBalance();
    double maxDrawdownThreshold = baseBalance * (1.0 - (MaxDrawdown / 100.0));
    if (eq <= maxDrawdownThreshold)
    {
        CloseAllPositions();
        _stopTrading = true;
        PrintLocal($"HARD STOP: Max Drawdown -{MaxDrawdown}% hit | Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2} from base ${baseBalance:F2}");
        return;
    }

    // Daily loss hard stop (already correct)
    double dailyLoss = _dailyStartBalance - eq;
    double dailyLossPercent = (dailyLoss / _dailyStartBalance) * 100.0;
    if (dailyLossPercent >= MaxDailyLoss)
    {
        CloseAllPositions();
        _stopTrading = true;
        PrintLocal($"HARD STOP: Daily Loss -{dailyLossPercent:F2}% >= {MaxDailyLoss}% | Equity={eq:F2}");
        return;
    }
}
```

---

## Testing Checklist

### Fixed Risk Mode Tests
- [ ] Start bot with Fixed Risk mode (Percent)
- [ ] Verify max drawdown is calculated from actual starting balance (not configured parameter)
- [ ] Verify log shows correct base balance for drawdown calculations
- [ ] Test drawdown gate triggers at correct threshold
- [ ] Verify daily loss calculation still works correctly

### Dynamic Risk Mode Tests
- [ ] Start bot with Dynamic Risk mode
- [ ] Verify max drawdown is calculated from configured Starting Balance parameter
- [ ] Verify risk scaling works correctly with configured balance
- [ ] Test drawdown gate triggers at correct threshold
- [ ] Verify compounding/non-compounding behavior

### Edge Cases
- [ ] Test with Starting Balance = 0 (auto-detect)
- [ ] Test with Starting Balance > actual balance
- [ ] Test with Starting Balance < actual balance
- [ ] Verify behavior after daily reset
- [ ] Test switching between risk modes (requires bot restart)

---

## Files to Modify

1. **SkyAnalyst Automated Trader.cs**
   - Add `_actualStartingBalance` field
   - Update `OnStart()` initialization
   - Add `GetDrawdownBaseBalance()` helper method
   - Update `CanTrade()` method
   - Update `CheckHardStops()` method

---

## Notes

- Daily loss calculation is already correct and doesn't need changes
- `_effectiveStartingBalance` should remain for Dynamic Risk scaling calculations
- The fix maintains backward compatibility for Dynamic Risk mode
- Enhanced logging will help users understand which balance is being used
- No changes needed to parameters or UI
