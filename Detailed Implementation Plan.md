Detailed Implementation Plan
Phase 1: Add New Enums and Parameters
1.1 Create New Enums

csharp
public enum RiskModeType
{
    Fixed,
    Dynamic
}
public enum FixedRiskUnit
{
    Percent,
    FixedDollar
}
1.2 Replace Existing Parameters

Remove:

StartingBalance
StartingRiskPercent
MaxRiskPercent
MaxLossPercent
DailyStopLossPercent
Add new parameter groups:

Group: "Risk Mode"

csharp
[Parameter("Risk Mode", Group = "Risk Mode", DefaultValue = RiskModeType.Fixed)]
public RiskModeType RiskMode { get; set; }
Group: "Fixed Risk Settings"

csharp
[Parameter("Fixed Risk Unit", Group = "Fixed Risk Settings", DefaultValue = FixedRiskUnit.Percent)]
public FixedRiskUnit FixedRiskUnit { get; set; }
[Parameter("Fixed Risk (%)", Group = "Fixed Risk Settings", DefaultValue = 1.0)]
public double FixedRiskPercent { get; set; }
[Parameter("Fixed Risk ($)", Group = "Fixed Risk Settings", DefaultValue = 100.0)]
public double FixedRiskDollar { get; set; }
Group: "Dynamic Risk Settings"

csharp
[Parameter("Starting Balance", Group = "Dynamic Risk Settings", DefaultValue = 1000)]
public double StartingBalance { get; set; }
[Parameter("Base Risk (%)", Group = "Dynamic Risk Settings", DefaultValue = 1.0)]
public double BaseRiskPercent { get; set; }
[Parameter("Max Risk (%)", Group = "Dynamic Risk Settings", DefaultValue = 2.0)]
public double MaxRiskPercent { get; set; }
Group: "Risk Scaling on Growth"

csharp
[Parameter("Equity Growth Step (%)", Group = "Risk Scaling on Growth", DefaultValue = 3.0)]
public double EquityGrowthStep { get; set; }
[Parameter("Risk Increase per Step (%)", Group = "Risk Scaling on Growth", DefaultValue = 20.0)]
public double RiskIncreasePerStep { get; set; }
[Parameter("Risk Compounding", Group = "Risk Scaling on Growth", DefaultValue = true)]
public bool RiskCompounding { get; set; }
Group: "Risk Reduction on Drawdown"

csharp
[Parameter("Drawdown Step (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 5.0)]
public double DrawdownStep { get; set; }
[Parameter("Risk Reduction per Step (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 50.0)]
public double RiskReductionPerStep { get; set; }
[Parameter("Max Drawdown (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 15.0)]
public double MaxDrawdown { get; set; }
[Parameter("Max Daily Loss (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 5.0)]
public double MaxDailyLoss { get; set; }
Phase 2: Refactor Risk Calculation Logic
2.1 Create New Method: CalculateRiskAmount()

Replace GetDynamicRiskPercent() with a more comprehensive method:

csharp
private double CalculateRiskAmount(double slDistancePips)
{
    if (RiskMode == RiskModeType.Fixed)
    {
        return CalculateFixedRiskAmount(slDistancePips);
    }
    else
    {
        return CalculateDynamicRiskAmount(slDistancePips);
    }
}
private double CalculateFixedRiskAmount(double slDistancePips)
{
    if (FixedRiskUnit == FixedRiskUnit.Percent)
    {
        return Account.Balance * (FixedRiskPercent / 100.0);
    }
    else
    {
        return FixedRiskDollar;
    }
}
private double CalculateDynamicRiskAmount(double slDistancePips)
{
    double currentBalance = Account.Balance;
    double balanceDiff = currentBalance - StartingBalance;
    double balanceDiffPercent = (balanceDiff / StartingBalance) * 100.0;
    
    double effectiveRisk = BaseRiskPercent;
    
    // Apply growth scaling
    if (balanceDiffPercent > 0)
    {
        effectiveRisk = CalculateGrowthScaledRisk(balanceDiffPercent);
    }
    // Apply drawdown reduction
    else if (balanceDiffPercent < 0)
    {
        effectiveRisk = CalculateDrawdownReducedRisk(balanceDiffPercent);
    }
    
    // Cap at max risk
    if (effectiveRisk > MaxRiskPercent)
        effectiveRisk = MaxRiskPercent;
    
    // Calculate risk amount
    double baseBalance = RiskCompounding ? currentBalance : StartingBalance;
    return baseBalance * (effectiveRisk / 100.0);
}
private double CalculateGrowthScaledRisk(double growthPercent)
{
    // Number of growth steps achieved
    double stepsAchieved = Math.Floor(growthPercent / EquityGrowthStep);
    
    // Calculate risk increase
    double riskIncrease = stepsAchieved * (RiskIncreasePerStep / 100.0) * BaseRiskPercent;
    
    return BaseRiskPercent + riskIncrease;
}
private double CalculateDrawdownReducedRisk(double drawdownPercent)
{
    double absDrawdown = Math.Abs(drawdownPercent);
    
    // Number of drawdown steps hit
    double stepsHit = Math.Floor(absDrawdown / DrawdownStep);
    
    // Calculate risk reduction (compound reduction)
    double reductionFactor = Math.Pow(1.0 - (RiskReductionPerStep / 100.0), stepsHit);
    
    return BaseRiskPercent * reductionFactor;
}
2.2 Add Helper Method for Display

csharp
public double GetCurrentRiskPercent()
{
    if (RiskMode == RiskModeType.Fixed && FixedRiskUnit == FixedRiskUnit.Percent)
    {
        return FixedRiskPercent;
    }
    else if (RiskMode == RiskModeType.Fixed && FixedRiskUnit == FixedRiskUnit.FixedDollar)
    {
        return (FixedRiskDollar / Account.Balance) * 100.0;
    }
    else
    {
        // Dynamic mode - calculate current effective risk
        double currentBalance = Account.Balance;
        double balanceDiff = currentBalance - StartingBalance;
        double balanceDiffPercent = (balanceDiff / StartingBalance) * 100.0;
        
        double effectiveRisk = BaseRiskPercent;
        
        if (balanceDiffPercent > 0)
            effectiveRisk = CalculateGrowthScaledRisk(balanceDiffPercent);
        else if (balanceDiffPercent < 0)
            effectiveRisk = CalculateDrawdownReducedRisk(balanceDiffPercent);
        
        if (effectiveRisk > MaxRiskPercent)
            effectiveRisk = MaxRiskPercent;
            
        return effectiveRisk;
    }
}
Phase 3: Update Position Sizing Methods
3.1 Update CalculateWebhookPositionSizes()

csharp
private void CalculateWebhookPositionSizes(
    double slDistancePips,
    out double pos1Volume,
    out double pos2Volume,
    out double pos3Volume,
    bool hasTP3)
{
    double riskAmount = CalculateRiskAmount(slDistancePips);
    
    double totalVolume = riskAmount / (slDistancePips * Symbol.PipValue);
    totalVolume = Symbol.NormalizeVolumeInUnits(totalVolume, RoundingMode.ToNearest);
    
    // Rest remains the same...
}
3.2 Update ExecuteManualTrade()

Replace:

csharp
double fr = CanTrade() ? GetDynamicRiskPercent() : 0.0;
double riskAmt = Account.Balance * (fr / 100.0);
With:

csharp
double riskAmt = CanTrade() ? CalculateRiskAmount(slPips) : 0.0;
3.3 Update Trading Panel UpdateValues()

Replace:

csharp
double fr = _bot.CanTrade() ? _bot.GetDynamicRiskPercent() : 0.0;
double riskAmt = _account.Balance * (fr / 100.0);
With:

csharp
double riskAmt = _bot.CanTrade() ? _bot.CalculateRiskAmount(slPips) : 0.0;
double fr = _bot.GetCurrentRiskPercent();
Phase 4: Update Risk Gates
4.1 Update CanTrade() Method

csharp
public bool CanTrade()
{
    if (_stopTrading) return false;
    double eq = Account.Equity;
    
    // Max drawdown check
    double maxDrawdownThreshold = StartingBalance * (1.0 - (MaxDrawdown / 100.0));
    if (eq <= maxDrawdownThreshold)
    {
        PrintLocal($"Max Drawdown hit: Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2}");
        return false;
    }
    // Daily loss check
    double dailyLoss = _dailyStartBalance - eq;
    double dailyLossPercent = (dailyLoss / _dailyStartBalance) * 100.0;
    if (dailyLossPercent >= MaxDailyLoss)
    {
        PrintLocal($"Daily Loss limit hit: {dailyLossPercent:F2}% >= {MaxDailyLoss:F2}%");
        return false;
    }
    return true;
}
Phase 5: Update Logging
5.1 Update Startup Log

csharp
if (RiskMode == RiskModeType.Fixed)
{
    if (FixedRiskUnit == FixedRiskUnit.Percent)
        PrintLocal($"Risk Mode: Fixed {FixedRiskPercent:F2}% per trade");
    else
        PrintLocal($"Risk Mode: Fixed ${FixedRiskDollar:F2} per trade");
}
else
{
    PrintLocal($"Risk Mode: Dynamic | Base: {BaseRiskPercent:F2}%, Max: {MaxRiskPercent:F2}%");
    PrintLocal($"Growth Scaling: +{RiskIncreasePerStep:F0}% per {EquityGrowthStep:F1}% growth");
    PrintLocal($"Drawdown Reduction: -{RiskReductionPerStep:F0}% per {DrawdownStep:F1}% DD");
    PrintLocal($"Compounding: {(RiskCompounding ? "Enabled" : "Disabled")}");
}
5.2 Update Webhook Execution Log

csharp
PrintLocal($"[WEBHOOK] Executed: {positionIds.Count} position(s) opened (IDs: {string.Join(", ", positionIds)}) | " +
          $"Risk: {GetCurrentRiskPercent():F2}% (${riskAmount:F2}), Total: {totalVolume} units");