/*
 * ================================================================================
 * Trading Panel 2.0 - Server
 * ================================================================================
 * 
 * Copyright © 2025 SkyAnalyst AI LLC
 * All Rights Reserved
 * 
 * Website: www.skyanalyst.ai
 * 
 * PROPRIETARY AND CONFIDENTIAL
 * 
 * This software and associated documentation files (the "Software") are the 
 * proprietary property of SkyAnalyst AI LLC. Unauthorized copying, distribution,
 * modification, or use of this Software, via any medium, is strictly prohibited
 * without the express written permission of SkyAnalyst AI LLC.
 * 
 * The Software is provided "as is", without warranty of any kind, express or
 * implied, including but not limited to the warranties of merchantability,
 * fitness for a particular purpose and noninfringement.
 * 
 * For licensing inquiries, please contact: www.skyanalyst.ai
 * ================================================================================
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using cAlgo;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum SLCalculationMode
    {
        Pip_Dist,
        Price
    }

    public enum JSONSLFormat
    {
        Pips,
        Price
    }

    public enum TPModificationMode
    {
        Pip_Diff,
        Price
    }

    public enum SLModificationMode
    {
        Pip_Diff,
        Price
    }

    public static class Styles
    {
        public static Style CreatePanelBackgroundStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#292929"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#FFFFFF"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));
            return style;
        }

        public static Style CreateInputStyle()
        {
            var style = new Style(DefaultStyles.TextBoxStyle);
            style.Set(ControlProperty.CornerRadius, 3);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#1A1A1A"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#E7EBED"), ControlState.LightTheme);
            return style;
        }

        public static Style CreateButtonStyle(Color mainColor, Color hoverColor)
        {
            var style = new Style(DefaultStyles.ButtonStyle);
            style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.LightTheme);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme);

            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, hoverColor, ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme | ControlState.Hover);

            return style;
        }

        public static Style CreatePlaceOrderButtonStyle(Color greenColor)
        {
            var style = new Style(DefaultStyles.ButtonStyle);
            style.Set(ControlProperty.BackgroundColor, greenColor, ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, greenColor, ControlState.LightTheme);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme);

            style.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme | ControlState.Hover);

            style.Set(ControlProperty.BorderColor, Color.Transparent);
            style.Set(ControlProperty.BorderThickness, 2);
            return style;
        }

        public static Style CreateLightGreyButtonStyle()
        {
            var style = new Style(DefaultStyles.ButtonStyle);

            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D3D3D3"), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromHex("#D3D3D3"), ControlState.LightTheme);
            style.Set(ControlProperty.ForegroundColor, Color.Black, ControlState.DarkTheme);
            style.Set(ControlProperty.ForegroundColor, Color.Black, ControlState.LightTheme);

            style.Set(ControlProperty.BorderColor, Color.Gray, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.Gray, ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, 1, ControlState.DarkTheme);
            style.Set(ControlProperty.BorderThickness, 1, ControlState.LightTheme);

            style.Set(ControlProperty.BackgroundColor, Color.Silver, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.BackgroundColor, Color.Silver, ControlState.LightTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.Black, ControlState.DarkTheme | ControlState.Hover);
            style.Set(ControlProperty.ForegroundColor, Color.Black, ControlState.LightTheme | ControlState.Hover);

            return style;
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class NewsTradePanelWWebhook : Robot
    {
        [Parameter("Account", DefaultValue = "FTMO 1")]
        public string AccountName { get; set; }

        [Parameter("Starting Balance", Group = "Risk Management", DefaultValue = 40000)]
        public double StartingBalance { get; set; }

        [Parameter("Starting Risk (%)", Group = "Risk Management", DefaultValue = 1.0)]
        public double StartingRiskPercent { get; set; }

        [Parameter("Max Risk (%)", Group = "Risk Management", DefaultValue = 2.0)]
        public double MaxRiskPercent { get; set; }

        [Parameter("Max Loss (%)", Group = "Risk Management", DefaultValue = 15.0)]
        public double MaxLossPercent { get; set; }

        [Parameter("Daily Stop Loss (%)", Group = "Risk Management", DefaultValue = 2.0)]
        public double DailyStopLossPercent { get; set; }

        [Parameter("Reverse Window (minutes)", Group = "Risk Management", DefaultValue = 10)]
        public int ReverseMinutes { get; set; }

        [Parameter("TP1 %", Group = "Trade Parameters", DefaultValue = 30.0)]
        public double TP1Percent { get; set; }

        [Parameter("TP2 %", Group = "Trade Parameters", DefaultValue = 30.0)]
        public double TP2Percent { get; set; }

        [Parameter("TP3 %", Group = "Trade Parameters", DefaultValue = 40.0)]
        public double TP3Percent { get; set; }

        [Parameter("TP1 R", Group = "Trade Parameters", DefaultValue = 1.0)]
        public double TP1R { get; set; }

        [Parameter("TP2 R", Group = "Trade Parameters", DefaultValue = 2.0)]
        public double TP2R { get; set; }

        [Parameter("TP3 R", Group = "Trade Parameters", DefaultValue = 10.0)]
        public double TP3R { get; set; }

        [Parameter("TSL Enabled", Group = "Trade Parameters", DefaultValue = true)]
        public bool TSL_Enabled { get; set; }

        [Parameter("TSL R Trigger", Group = "Trade Parameters", DefaultValue = 2.0)]
        public double TSL_R_Trigger { get; set; }

        [Parameter("TSL R Distance", Group = "Trade Parameters", DefaultValue = 2.0)]
        public double TSL_R_Distance { get; set; }

        [Parameter("Default Stop Loss (pips)", Group = "Trade Parameters", DefaultValue = 20)]
        public double DefaultStopLossPips { get; set; }

        [Parameter("Default SL Mode", Group = "Trade Parameters", DefaultValue = SLCalculationMode.Price)]
        public SLCalculationMode DefaultSLMode { get; set; }

        [Parameter("Max Positive Trades/Day", Group = "Daily Limits", DefaultValue = 10)]
        public int MaxPositiveTradesDay { get; set; }

        [Parameter("Max Negative Trades/Day", Group = "Daily Limits", DefaultValue = 10)]
        public int MaxNegativeTradesDay { get; set; }

        [Parameter("Bot Log Folder", Group = "Logging",
            DefaultValue = @"C:\Users\juanc\iCloudDrive\Trading\US30 Terminator Bot\Bot Log")]
        public string LogFolder { get; set; }

        // ------------------------ "Server Parameters" ------------------------
        [Parameter("Ports Active (1-4)", Group = "Server Parameters", DefaultValue = 1)]
        public int PortsActive { get; set; }

        [Parameter("Port 1", Group = "Server Parameters", DefaultValue = 8301)]
        public int Port1 { get; set; }

        [Parameter("Port 2", Group = "Server Parameters", DefaultValue = 8302)]
        public int Port2 { get; set; }

        [Parameter("Port 3", Group = "Server Parameters", DefaultValue = 8303)]
        public int Port3 { get; set; }

        [Parameter("Port 4", Group = "Server Parameters", DefaultValue = 8304)]
        public int Port4 { get; set; }

        [Parameter("JSON SL Format", Group = "Server Parameters", DefaultValue = JSONSLFormat.Pips)]
        public JSONSLFormat JsonSLFormat { get; set; }
        // -----------------------------------------------------------------------

        private bool _stopTrading;
        private double _dailyStartBalance;
        private DateTime _currentDayLocal;
        private TimeZoneInfo _ecuadorTimeZone;
        private bool _manualPositionOpen;
        private double _manualTradeCumulativeProfit;
        private int _dailyPosTrades;
        private int _dailyNegTrades;
        private MultiPositionPartialTPManager _tradeManager;
        private List<string> _logs = new List<string>();
        private Dictionary<long, double?> _positionTPLevels = new Dictionary<long, double?>();
        private Dictionary<long, double?> _positionSLLevels = new Dictionary<long, double?>();

        protected override void OnStart()
        {
            try
            {
                _ecuadorTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
            }
            catch
            {
                Print("TimeZone not found => fallback UTC");
                _ecuadorTimeZone = TimeZoneInfo.Utc;
            }

            var nowLocalEcu = TimeZoneInfo.ConvertTimeFromUtc(Server.TimeInUtc, _ecuadorTimeZone);

            PrintLocal($"Starting Bot for Account='{AccountName}'...");

            double initialRiskNow = GetDynamicRiskPercent();
            PrintLocal($"Startup => StartingBalance={StartingBalance:F2}, " +
                       $"CurrentBalance={Account.Balance:F2}, " +
                       $"ConfiguredRisk={StartingRiskPercent:F2}%, " +
                       $"DynamicRiskNow={initialRiskNow:F2}%");

            _dailyStartBalance = Account.Balance;
            _currentDayLocal = nowLocalEcu.Date;
            _manualPositionOpen = false;
            _manualTradeCumulativeProfit = 0.0;
            _dailyPosTrades = 0;
            _dailyNegTrades = 0;

            var tradingPanel = new TradingPanel(
                this,
                Account,
                Symbol,
                DefaultStopLossPips,
                DefaultSLMode,
                1.0,
                TP1Percent,
                TP2Percent,
                TP3Percent,
                TP1R,
                TP2R,
                TP3R
            );
            var mainBorder = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = Styles.CreatePanelBackgroundStyle(),
                Margin = "0 0 0 20"
            };
            mainBorder.Child = tradingPanel;
            Chart.AddControl(mainBorder);

            _tradeManager = new MultiPositionPartialTPManager(
                this,
                TP1Percent, TP2Percent, TP3Percent,
                TP1R, TP2R, TP3R,
                TSL_Enabled, TSL_R_Trigger, TSL_R_Distance
            );

            Positions.Closed += OnPositionClosed;

            bool leftoverPartial = Positions.Any(pos =>
                pos.SymbolName == SymbolName &&
                (pos.Label == "TP1Position" || pos.Label == "TP2Position" || pos.Label == "TP3Position") &&
                pos.Quantity > 0
            );
            if (leftoverPartial)
            {
                _manualPositionOpen = true;
                PrintLocal("Detected leftover partial positions => blocking new manual trades.");
            }

            _tradeManager.CheckPositions();
        }

        protected override void OnTick()
        {
            CheckDayChange();
            CheckHardStops();
            _tradeManager.CheckPositions();
            CheckTPModifications();
            CheckSLModifications();
        }

        protected override void OnStop()
        {
            WriteLogsToFile();
            PrintLocal("Bot stopped.");
        }

        public bool CanTrade()
        {
            if (_stopTrading) return false;

            double eq = Account.Equity;
            double globalDD = StartingBalance * (1.0 - (MaxLossPercent / 100.0));
            if (eq <= globalDD)
            {
                PrintLocal($"Gate => eq<{globalDD:F2} => global -{MaxLossPercent}% => no new trades");
                return false;
            }

            double dailyDD = _dailyStartBalance * (1.0 - (DailyStopLossPercent / 100.0));
            if (eq <= dailyDD)
            {
                PrintLocal($"Gate => eq<{dailyDD:F2} => daily -{DailyStopLossPercent}% => no new trades");
                return false;
            }

            return true;
        }

        // -----------------------------------------------------------------------
        // Send HTTP messages to the configured ports:
        // action: "Buy"/"Sell" (with sl_pips or sl_price), or "Close All Positions"
        // -----------------------------------------------------------------------
        private void SendJsonMessage(string action, double? slPips, double? slPrice, string symbol, JSONSLFormat format)
        {
            var messageObj = new Dictionary<string, object>
            {
                ["action"] = action,
                ["symbol"] = symbol
            };
            
            // Add SL fields - always send both formats
            if (slPips.HasValue)
                messageObj["sl_pips"] = slPips.Value;
            if (slPrice.HasValue)
                messageObj["sl_price"] = slPrice.Value;

            // Add current price for reference
            if (action == "Buy" || action == "Sell")
            {
                TradeType tradeType = (action == "Buy") ? TradeType.Buy : TradeType.Sell;
                double currentPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                messageObj["current_price"] = currentPrice;
                
                // Add TP levels for all 3 positions
                if (slPips.HasValue)
                {
                    var tpLevels = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["label"] = "TP1Position",
                            ["r_value"] = TP1R,
                            ["tp_pips"] = slPips.Value * TP1R,
                            ["tp_price"] = CalculateTPPrice(tradeType, currentPrice, slPips.Value * TP1R)
                        },
                        new Dictionary<string, object>
                        {
                            ["label"] = "TP2Position",
                            ["r_value"] = TP2R,
                            ["tp_pips"] = slPips.Value * TP2R,
                            ["tp_price"] = CalculateTPPrice(tradeType, currentPrice, slPips.Value * TP2R)
                        },
                        new Dictionary<string, object>
                        {
                            ["label"] = "TP3Position",
                            ["r_value"] = TP3R,
                            ["tp_pips"] = slPips.Value * TP3R,
                            ["tp_price"] = CalculateTPPrice(tradeType, currentPrice, slPips.Value * TP3R)
                        }
                    };
                    messageObj["tp_levels"] = tpLevels;
                    PrintLocal($"Broadcasted Trade: {action}, SL={slPips.Value:F1} pips, TP1={TP1R}R, TP2={TP2R}R, TP3={TP3R}R");
                }
            }

            string json = JsonSerializer.Serialize(messageObj);

            List<int> portsToUse = GetActivePorts();
            foreach (var port in portsToUse)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        // If your code uses /newtrade path, adapt here if needed:
                        var url = $"http://localhost:{port}/newtrade"; 
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var resp = client.PostAsync(url, content).Result;
                        if (!resp.IsSuccessStatusCode)
                            PrintLocal($"SendJsonMessage => port={port}, status={resp.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    PrintLocal($"SendJsonMessage error => port={port}, {ex.Message}");
                }
            }
        }

        private double CalculateTPPrice(TradeType tradeType, double entryPrice, double tpPips)
        {
            double tpDistance = tpPips * Symbol.PipSize;
            
            if (tradeType == TradeType.Buy)
                return entryPrice + tpDistance;
            else
                return entryPrice - tpDistance;
        }

        private List<int> GetActivePorts()
        {
            var ports = new List<int>();
            if (PortsActive >= 1) ports.Add(Port1);
            if (PortsActive >= 2) ports.Add(Port2);
            if (PortsActive >= 3) ports.Add(Port3);
            if (PortsActive >= 4) ports.Add(Port4);
            return ports;
        }
        // -----------------------------------------------------------------------

        public void ExecuteManualTrade(TradeType tradeType, double slPips,
                                       double tp1Percent, double tp2Percent, double tp3Percent,
                                       double tp1R, double tp2R, double tp3R)
        {
            if (!CanTrade())
            {
                PrintLocal("Manual trade blocked => equity below threshold or stopTrading=true");
                return;
            }

            PrintLocal($"Manual trade => partial TPs => {tradeType}, slPips={slPips:F1}");

            double finalDist = (slPips <= 0) ? DefaultStopLossPips : slPips;
            double fr = GetDynamicRiskPercent();
            if (fr <= 0) return;

            double riskAmt = Account.Balance * (fr / 100.0);
            double volumeUnits = riskAmt / (finalDist * Symbol.PipValue);
            volumeUnits = Symbol.NormalizeVolumeInUnits(volumeUnits, RoundingMode.ToNearest);
            if (volumeUnits < Symbol.VolumeInUnitsMin)
                volumeUnits = Symbol.VolumeInUnitsMin;

            double pos1 = volumeUnits * (tp1Percent / 100.0);
            double pos2 = volumeUnits * (tp2Percent / 100.0);
            double pos3 = volumeUnits * (tp3Percent / 100.0);

            pos1 = Symbol.NormalizeVolumeInUnits(pos1, RoundingMode.ToNearest);
            pos2 = Symbol.NormalizeVolumeInUnits(pos2, RoundingMode.ToNearest);
            pos3 = Symbol.NormalizeVolumeInUnits(pos3, RoundingMode.ToNearest);

            if (pos1 < Symbol.VolumeInUnitsMin) pos1 = Symbol.VolumeInUnitsMin;
            if (pos2 < Symbol.VolumeInUnitsMin) pos2 = Symbol.VolumeInUnitsMin;
            if (pos3 < Symbol.VolumeInUnitsMin) pos3 = Symbol.VolumeInUnitsMin;

            double tp1 = finalDist * tp1R;
            double tp2 = finalDist * tp2R;
            double tp3 = finalDist * tp3R;

            ExecuteMarketOrderAsync(tradeType, SymbolName, pos1, "TP1Position", finalDist, tp1);
            ExecuteMarketOrderAsync(tradeType, SymbolName, pos2, "TP2Position", finalDist, tp2);
            ExecuteMarketOrderAsync(tradeType, SymbolName, pos3, "TP3Position", finalDist, tp3);

            // Calculate SL price for JSON message
            double currentPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
            double slPrice = (tradeType == TradeType.Buy) 
                ? currentPrice - (finalDist * Symbol.PipSize) 
                : currentPrice + (finalDist * Symbol.PipSize);

            string act = (tradeType == TradeType.Buy) ? "Buy" : "Sell";
            SendJsonMessage(act, slPips, slPrice, SymbolName, JsonSLFormat);

            _manualPositionOpen = true;
            _manualTradeCumulativeProfit = 0;
        }

        public void CloseAllPositions()
        {
            SendJsonMessage("Close All Positions", null, null, SymbolName, JsonSLFormat);

            foreach (var pos in Positions)
                if (pos.SymbolName == SymbolName)
                    ClosePositionAsync(pos);
        }

        private void OnPositionClosed(PositionClosedEventArgs e)
        {
            var pos = e.Position;

            BeginInvokeOnMainThread(() =>
            {
                if (_manualPositionOpen && pos.Label.StartsWith("TP", StringComparison.OrdinalIgnoreCase))
                {
                    _manualTradeCumulativeProfit += pos.NetProfit;
                    var remain = Positions.Count(p => p.SymbolName == SymbolName
                                           && p.Label.StartsWith("TP", StringComparison.OrdinalIgnoreCase)
                                           && p.Quantity > 0);
                    if (remain == 0)
                    {
                        _manualPositionOpen = false;
                        if (_manualTradeCumulativeProfit > 0)
                        {
                            _dailyPosTrades++;
                            PrintLocal($"Manual partial => posTrades={_dailyPosTrades}/{MaxPositiveTradesDay}");
                            if (_dailyPosTrades >= MaxPositiveTradesDay)
                            {
                                PrintLocal("Daily + trade limit => stopping");
                                CloseAllPositions();
                                _stopTrading = true;
                            }
                        }
                        else if (_manualTradeCumulativeProfit < 0)
                        {
                            _dailyNegTrades++;
                            PrintLocal($"Manual partial => negTrades={_dailyNegTrades}/{MaxNegativeTradesDay}");
                            if (_dailyNegTrades >= MaxNegativeTradesDay)
                            {
                                PrintLocal("Daily - trade limit => stopping");
                                CloseAllPositions();
                                _stopTrading = true;
                            }
                        }
                        _manualTradeCumulativeProfit = 0;
                    }
                    
                    // Broadcast individual position closure to receivers
                    BroadcastPositionClosure(pos);
                }
                
                // Clean up TP and SL tracking dictionaries
                if (_positionTPLevels.ContainsKey(pos.Id))
                    _positionTPLevels.Remove(pos.Id);
                
                if (_positionSLLevels.ContainsKey(pos.Id))
                    _positionSLLevels.Remove(pos.Id);
            });
        }

        private void CheckDayChange()
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(Server.TimeInUtc, _ecuadorTimeZone).Date;
            if (localNow > _currentDayLocal)
            {
                WriteLogsToFile();

                _currentDayLocal = localNow;
                _dailyStartBalance = Account.Balance;
                _dailyPosTrades = 0;
                _dailyNegTrades = 0;
                _stopTrading = false;

                _manualTradeCumulativeProfit = 0;
                _manualPositionOpen = false;

                _logs.Clear();
            }
        }

        private void CheckHardStops()
        {
            if (_stopTrading) return;

            double eq = Account.Equity;
            double globalDD = StartingBalance * (1.0 - (MaxLossPercent / 100.0));
            if (eq <= globalDD)
            {
                CloseAllPositions();
                _stopTrading = true;
                PrintLocal($"Global stop => -{MaxLossPercent}% => eq={eq:F2}");
                return;
            }

            double dailyDD = _dailyStartBalance * (1.0 - (DailyStopLossPercent / 100.0));
            if (eq <= dailyDD)
            {
                CloseAllPositions();
                _stopTrading = true;
                PrintLocal($"Daily stop => -{DailyStopLossPercent}% => eq={eq:F2}");
                return;
            }
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
            
            var msg = new Dictionary<string, object>
            {
                ["action"] = "ModifyTP",
                ["symbol"] = SymbolName,
                ["position_label"] = pos.Label,
                ["trade_type"] = pos.TradeType.ToString(),
                ["tp_price"] = tpPrice,
                ["tp_pip_diff"] = tpPipDiff,
                ["current_price"] = currentPrice,
                ["entry_price"] = pos.EntryPrice
            };
            
            string jsonMsg = JsonSerializer.Serialize(msg);
            
            List<int> portsToUse = GetActivePorts();
            foreach (var port in portsToUse)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var url = $"http://localhost:{port}/newtrade";
                        var content = new StringContent(jsonMsg, Encoding.UTF8, "application/json");
                        var resp = client.PostAsync(url, content).Result;
                        if (!resp.IsSuccessStatusCode)
                            PrintLocal($"BroadcastTPMod => port={port}, status={resp.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    PrintLocal($"BroadcastTPMod error => port={port}, {ex.Message}");
                }
            }
            
            PrintLocal($"Broadcasted TP Mod: {pos.Label}, Price={tpPrice:F5}, PipDiff={tpPipDiff:F1}");
        }

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
            
            var msg = new Dictionary<string, object>
            {
                ["action"] = "ModifySL",
                ["symbol"] = SymbolName,
                ["position_label"] = pos.Label,
                ["trade_type"] = pos.TradeType.ToString(),
                ["sl_price"] = slPrice,
                ["sl_pip_diff"] = slPipDiff,
                ["current_price"] = currentPrice,
                ["entry_price"] = pos.EntryPrice
            };
            
            string jsonMsg = JsonSerializer.Serialize(msg);
            
            List<int> portsToUse = GetActivePorts();
            foreach (var port in portsToUse)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var url = $"http://localhost:{port}/newtrade";
                        var content = new StringContent(jsonMsg, Encoding.UTF8, "application/json");
                        var resp = client.PostAsync(url, content).Result;
                        if (!resp.IsSuccessStatusCode)
                            PrintLocal($"BroadcastSLMod => port={port}, status={resp.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    PrintLocal($"BroadcastSLMod error => port={port}, {ex.Message}");
                }
            }
            
            PrintLocal($"Broadcasted SL Mod: {pos.Label}, Price={slPrice:F5}, PipDiff={slPipDiff:F1}");
        }

        private void BroadcastPositionClosure(Position pos)
        {
            var msg = new Dictionary<string, object>
            {
                ["action"] = "ClosePosition",
                ["symbol"] = SymbolName,
                ["position_label"] = pos.Label,
                ["trade_type"] = pos.TradeType.ToString()
            };
            
            string jsonMsg = JsonSerializer.Serialize(msg);
            
            List<int> portsToUse = GetActivePorts();
            foreach (var port in portsToUse)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        var url = $"http://localhost:{port}/newtrade";
                        var content = new StringContent(jsonMsg, Encoding.UTF8, "application/json");
                        var resp = client.PostAsync(url, content).Result;
                        if (!resp.IsSuccessStatusCode)
                            PrintLocal($"BroadcastPositionClosure => port={port}, status={resp.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    PrintLocal($"BroadcastPositionClosure error => port={port}, {ex.Message}");
                }
            }
            
            PrintLocal($"Broadcasted Position Closure: {pos.Label}");
        }



        public double GetDynamicRiskPercent()
        {
            double bal = Account.Balance;
            double diff = bal - StartingBalance;
            double diffPercent = (diff / StartingBalance) * 100.0;
            double R = StartingRiskPercent;
            double newRisk = R;

            if (diffPercent < 0)
            {
                if (diffPercent >= -5.0 * (R / 1.0))
                    newRisk = R;
                else if (diffPercent >= -7.5 * (R / 1.0))
                    newRisk = R / 2.0;
                else
                    newRisk = R / 4.0;
            }
            else
            {
                double incrementsNeeded = Math.Floor(diffPercent / (2.0 * R));
                double extraRisk = incrementsNeeded * (0.2 * R);
                newRisk = R + extraRisk;
            }

            if (newRisk > MaxRiskPercent)
                newRisk = MaxRiskPercent;

            return newRisk;
        }



        private void PrintLocal(string msg)
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(Server.TimeInUtc, _ecuadorTimeZone);
            string finalMsg = $"{localNow:HH:mm} => {msg}";
            Print($"{AccountName}: {finalMsg}");
            _logs.Add($"{AccountName}: {finalMsg}");
        }

        private void WriteLogsToFile()
        {
            try
            {
                if (_logs.Count == 0) return;

                var nowLocalEcu = TimeZoneInfo.ConvertTimeFromUtc(Server.TimeInUtc, _ecuadorTimeZone);
                var fileName = $"{AccountName}_BotLog_{nowLocalEcu:yyyy-MM-dd_HH-mm-ss}.log";
                var fullPath = Path.Combine(LogFolder, fileName);

                Directory.CreateDirectory(LogFolder);
                File.WriteAllLines(fullPath, _logs);

                Print($"{AccountName}: Logs saved => {fullPath}");
            }
            catch (Exception ex)
            {
                Print($"WriteLogsToFile error => {ex.Message}");
            }
        }

    }

    public class TradingPanel : CustomControl
    {
        private readonly NewsTradePanelWWebhook _bot;
        private readonly IAccount _account;
        private readonly Symbol _symbol;
        private double TP1Percent;
        private double TP2Percent;
        private double TP3Percent;
        private double TP1R;
        private double TP2R;
        private double TP3R;
        private TextBox slTextBox;
        private TextBox priceTextBox;
        private TextBlock estimatedRiskTextBlock;
        private TextBlock riskPercentTextBlock;
        private TextBlock qtyTextBlock;
        private Button buyButton;
        private Button sellButton;
        private Button placeOrderButton;
        private Button closeAllButton;
        private Button pipsModeButton;
        private Button priceModeButton;
        private TradeType selectedTradeType = TradeType.Buy;
        private SLCalculationMode selectedSLMode;

        public TradingPanel(NewsTradePanelWWebhook bot,
                            IAccount account,
                            Symbol symbol,
                            double defaultStopLossPips,
                            SLCalculationMode defaultSLMode,
                            double defaultRiskPercent,
                            double tp1Percent,
                            double tp2Percent,
                            double tp3Percent,
                            double tp1R,
                            double tp2R,
                            double tp3r)
        {
            _bot = bot;
            _account = account;
            _symbol = symbol;
            selectedSLMode = defaultSLMode;

            TP1Percent = tp1Percent;
            TP2Percent = tp2Percent;
            TP3Percent = tp3Percent;
            TP1R = tp1R;
            TP2R = tp2R;
            TP3R = tp3r;

            AddChild(CreatePanel(defaultStopLossPips));
        }

        private ControlBase CreatePanel(double defaultStopLossPips)
        {
            var main = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = "10"
            };

            var closeStyle = new Style(DefaultStyles.ButtonStyle);
            closeStyle.Set(ControlProperty.BackgroundColor, Color.Red, ControlState.DarkTheme);
            closeStyle.Set(ControlProperty.BackgroundColor, Color.Red, ControlState.LightTheme);
            closeStyle.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme);
            closeStyle.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme);
            closeStyle.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.DarkTheme | ControlState.Hover);
            closeStyle.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.LightTheme | ControlState.Hover);
            closeStyle.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme | ControlState.Hover);
            closeStyle.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme | ControlState.Hover);
            closeStyle.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.Hover);
            closeStyle.Set(ControlProperty.BorderThickness, 0, ControlState.Hover);

            closeAllButton = new Button
            {
                Text = "Close\nPositions",
                Margin = "10 0 10 0",
                Style = closeStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            closeAllButton.Click += (args) => _bot.CloseAllPositions();
            main.AddChild(closeAllButton);

            var modeStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 0 10 0"
            };
            var modeLabel = new TextBlock { Text = "SL Mode" };
            modeStack.AddChild(modeLabel);

            var modeButtonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = "0 5 0 0"
            };
            pipsModeButton = new Button
            {
                Text = "Pips",
                Width = 50,
                Margin = "0 0 5 0"
            };
            pipsModeButton.Click += (args) => SelectSLMode(SLCalculationMode.Pip_Dist);
            modeButtonStack.AddChild(pipsModeButton);

            priceModeButton = new Button
            {
                Text = "Price",
                Width = 50
            };
            priceModeButton.Click += (args) => SelectSLMode(SLCalculationMode.Price);
            modeButtonStack.AddChild(priceModeButton);

            modeStack.AddChild(modeButtonStack);
            
            var brandingText = new TextBlock
            {
                Text = "SkyAnalyst Trading Panel",
                FontSize = 9,
                Margin = "0 8 0 0",
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.7
            };
            modeStack.AddChild(brandingText);
            
            main.AddChild(modeStack);

            var slStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 0 10 0"
            };
            var slLabel = new TextBlock { Text = "SL (pips)" };
            slStack.AddChild(slLabel);

            slTextBox = new TextBox
            {
                Text = defaultStopLossPips.ToString("F1"),
                Style = Styles.CreateInputStyle(),
                Width = 60,
                Margin = "0 5 0 0"
            };
            slTextBox.TextChanged += (args) => UpdateValues();
            slStack.AddChild(slTextBox);
            main.AddChild(slStack);

            var priceStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 0 10 0"
            };
            var priceLabel = new TextBlock { Text = "SL Price" };
            priceStack.AddChild(priceLabel);

            priceTextBox = new TextBox
            {
                Text = "0",
                Style = Styles.CreateInputStyle(),
                Width = 60,
                Margin = "0 5 0 0",
                IsEnabled = false
            };
            priceTextBox.TextChanged += (args) => UpdateValues();
            priceStack.AddChild(priceTextBox);
            main.AddChild(priceStack);

            var qtyRiskStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "10 0 10 0"
            };
            qtyTextBlock = new TextBlock
            {
                Text = "Qty: 00.00",
                FontSize = 11,
                Margin = "0 0 0 3"
            };
            estimatedRiskTextBlock = new TextBlock
            {
                Text = "Risk: $0.00",
                FontSize = 11,
                Margin = "0 3 0 3"
            };
            riskPercentTextBlock = new TextBlock
            {
                Text = "Risk: 0.00%",
                FontSize = 11,
                Margin = "0 3 0 0"
            };
            qtyRiskStack.AddChild(qtyTextBlock);
            qtyRiskStack.AddChild(estimatedRiskTextBlock);
            qtyRiskStack.AddChild(riskPercentTextBlock);
            main.AddChild(qtyRiskStack);

            buyButton = new Button
            {
                Text = "Buy",
                Margin = "10 0 10 0"
            };
            buyButton.Click += (args) => SelectDirection(TradeType.Buy);
            main.AddChild(buyButton);

            sellButton = new Button
            {
                Text = "Sell",
                Margin = "10 0 10 0"
            };
            sellButton.Click += (args) => SelectDirection(TradeType.Sell);
            main.AddChild(sellButton);

            var placeStyle = Styles.CreatePlaceOrderButtonStyle(Color.FromHex("#058000"));
            placeOrderButton = new Button
            {
                Text = "Place\nOrder",
                Margin = "10 0 0 0",
                Style = placeStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            placeOrderButton.Click += (args) => ExecuteManual();
            main.AddChild(placeOrderButton);

            SelectDirection(TradeType.Buy);
            SelectSLMode(selectedSLMode);
            UpdateValues();
            return main;
        }

        private void SelectSLMode(SLCalculationMode mode)
        {
            selectedSLMode = mode;
            
            if (mode == SLCalculationMode.Pip_Dist)
            {
                slTextBox.IsEnabled = true;
                priceTextBox.IsEnabled = false;
                ApplyButtonStyle(pipsModeButton, true, Color.FromHex("#666666"));
                ApplyButtonStyle(priceModeButton, false, Color.FromHex("#666666"));
            }
            else
            {
                slTextBox.IsEnabled = false;
                priceTextBox.IsEnabled = true;
                ApplyButtonStyle(pipsModeButton, false, Color.FromHex("#666666"));
                ApplyButtonStyle(priceModeButton, true, Color.FromHex("#666666"));
            }
            
            UpdateValues();
        }

        private void SelectDirection(TradeType t)
        {
            selectedTradeType = t;
            ApplyButtonStyle(buyButton, selectedTradeType == TradeType.Buy, Color.FromHex("#058000"));
            ApplyButtonStyle(sellButton, selectedTradeType == TradeType.Sell, Color.Red);
        }

        private void ApplyButtonStyle(Button button, bool isSelected, Color mainColor)
        {
            var style = new Style(DefaultStyles.ButtonStyle);

            if (isSelected)
            {
                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.DarkTheme);
                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.LightTheme);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme);
                style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.LightTheme);
                style.Set(ControlProperty.BorderThickness, 0, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderThickness, 0, ControlState.LightTheme);

                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.DarkTheme | ControlState.Hover);
                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.LightTheme | ControlState.Hover);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme | ControlState.Hover);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme | ControlState.Hover);
                style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.Hover);
                style.Set(ControlProperty.BorderThickness, 0, ControlState.Hover);
            }
            else
            {
                style.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.DarkTheme);
                style.Set(ControlProperty.BackgroundColor, Color.Black, ControlState.LightTheme);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme);

                style.Set(ControlProperty.BorderColor, mainColor, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderColor, mainColor, ControlState.LightTheme);
                style.Set(ControlProperty.BorderThickness, 2, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderThickness, 2, ControlState.LightTheme);

                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.DarkTheme | ControlState.Hover);
                style.Set(ControlProperty.BackgroundColor, mainColor, ControlState.LightTheme | ControlState.Hover);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.DarkTheme | ControlState.Hover);
                style.Set(ControlProperty.ForegroundColor, Color.White, ControlState.LightTheme | ControlState.Hover);
                style.Set(ControlProperty.BorderColor, Color.Transparent, ControlState.Hover);
                style.Set(ControlProperty.BorderThickness, 0, ControlState.Hover);
            }
            button.Style = style;
        }

        private void UpdateValues()
        {
            double slPips = 0;

            if (selectedSLMode == SLCalculationMode.Pip_Dist)
            {
                slPips = GetVal(slTextBox, 20);
            }
            else
            {
                double slPrice = GetVal(priceTextBox, 0);
                if (slPrice <= 0)
                {
                    estimatedRiskTextBlock.Text = "Risk: Invalid";
                    riskPercentTextBlock.Text = "Risk: Invalid";
                    qtyTextBlock.Text = "Qty: Invalid";
                    return;
                }

                double currentPrice = (selectedTradeType == TradeType.Buy) ? _symbol.Ask : _symbol.Bid;
                double priceDiff = Math.Abs(currentPrice - slPrice);
                slPips = priceDiff / _symbol.PipSize;
            }

            if (slPips <= 0)
            {
                estimatedRiskTextBlock.Text = "Risk: Invalid";
                riskPercentTextBlock.Text = "Risk: Invalid";
                qtyTextBlock.Text = "Qty: Invalid";
                return;
            }

            double fr = _bot.CanTrade() ? _bot.GetDynamicRiskPercent() : 0.0;
            double riskAmt = _account.Balance * (fr / 100.0);

            double volumeUnits = (slPips > 0) ? riskAmt / (slPips * _symbol.PipValue) : 0;
            volumeUnits = _symbol.NormalizeVolumeInUnits(volumeUnits, RoundingMode.ToNearest);
            if (volumeUnits < _symbol.VolumeInUnitsMin)
                volumeUnits = _symbol.VolumeInUnitsMin;

            double lots = volumeUnits / _symbol.LotSize;
            estimatedRiskTextBlock.Text = $"Risk: ${riskAmt:F2}";
            riskPercentTextBlock.Text = $"Risk: {fr:F2}%";
            qtyTextBlock.Text = $"Qty: {lots:F2}";
        }

        private void ExecuteManual()
        {
            double slPips = 0;

            if (selectedSLMode == SLCalculationMode.Pip_Dist)
            {
                slPips = GetVal(slTextBox, 20);
            }
            else
            {
                double slPrice = GetVal(priceTextBox, 0);
                if (slPrice <= 0)
                {
                    return;
                }

                double currentPrice = (selectedTradeType == TradeType.Buy) ? _symbol.Ask : _symbol.Bid;
                double priceDiff = Math.Abs(currentPrice - slPrice);
                slPips = priceDiff / _symbol.PipSize;
            }

            _bot.ExecuteManualTrade(
                selectedTradeType,
                slPips,
                _bot.TP1Percent,
                _bot.TP2Percent,
                _bot.TP3Percent,
                _bot.TP1R,
                _bot.TP2R,
                _bot.TP3R
            );
        }

        private double GetVal(TextBox tb, double def)
        {
            if (double.TryParse(tb.Text, out double v)) return v;
            return def;
        }
    }

    public class MultiPositionPartialTPManager
    {
        private class ManagedPosition
        {
            public long Id { get; set; }
            public TradeType TradeType { get; set; }
            public double EntryPrice { get; set; }
            public double InitialSL { get; set; }
            public double RDistanceInPips { get; set; }
            public bool TrailingActivated { get; set; }
        }

        private readonly NewsTradePanelWWebhook _robot;
        private List<ManagedPosition> _mpos = new List<ManagedPosition>();
        private double TP1Percent;
        private double TP2Percent;
        private double TP3Percent;
        private double TP1R;
        private double TP2R;
        private double TP3R;
        private bool TSL_Enabled;
        private double TSL_R_Trigger;
        private double TSL_R_Distance;
        private bool _globalTrailing;

        public MultiPositionPartialTPManager(
            NewsTradePanelWWebhook robot,
            double tp1p, double tp2p, double tp3p,
            double tp1r, double tp2r, double tp3r,
            bool tslEnabled, double tslRTrigger, double tslRDistance)
        {
            _robot = robot;
            TP1Percent = tp1p;
            TP2Percent = tp2p;
            TP3Percent = tp3p;
            TP1R = tp1r;
            TP2R = tp2r;
            TP3R = tp3r;
            TSL_Enabled = tslEnabled;
            TSL_R_Trigger = tslRTrigger;
            TSL_R_Distance = tslRDistance;
            _globalTrailing = false;
        }

        public void CheckPositions()
        {
            DetectNewPositions();

            foreach (var mp in _mpos.ToList())
            {
                var currentPos = _robot.Positions.FirstOrDefault(p => p.Id == mp.Id);
                if (currentPos == null || currentPos.Quantity <= 0)
                {
                    _mpos.Remove(mp);
                    continue;
                }

                if (!_globalTrailing && TSL_Enabled)
                {
                    double curPrice = (mp.TradeType == TradeType.Buy) ? _robot.Symbol.Bid : _robot.Symbol.Ask;
                    double gainedPips = (mp.TradeType == TradeType.Buy)
                        ? (curPrice - mp.EntryPrice) / _robot.Symbol.PipSize
                        : (mp.EntryPrice - curPrice) / _robot.Symbol.PipSize;

                    double cR = gainedPips / mp.RDistanceInPips;
                    if (cR >= TSL_R_Trigger)
                    {
                        _globalTrailing = true;
                        _robot.Print($"Global trailing => triggered at {TSL_R_Trigger}R");
                    }
                }

                if (_globalTrailing && TSL_Enabled)
                {
                    double cp = (mp.TradeType == TradeType.Buy) ? _robot.Symbol.Bid : _robot.Symbol.Ask;
                    double desiredDist = mp.RDistanceInPips * TSL_R_Distance;
                    double newSL = (mp.TradeType == TradeType.Buy)
                        ? cp - (desiredDist * _robot.Symbol.PipSize)
                        : cp + (desiredDist * _robot.Symbol.PipSize);

                    bool needUpdate = false;
                    if (mp.TradeType == TradeType.Buy && newSL > currentPos.StopLoss)
                        needUpdate = true;
                    if (mp.TradeType == TradeType.Sell && newSL < currentPos.StopLoss)
                        needUpdate = true;

#pragma warning disable 0618
                    if (needUpdate)
                    {
                        var mod = _robot.ModifyPosition(currentPos, newSL, currentPos.TakeProfit);
                        if (!mod.IsSuccessful)
                            _robot.Print($"TSL update fail => pos {mp.Id}, {mod.Error}");
                        else
                            _robot.Print($"TSL updated => pos {mp.Id}, newSL={newSL:F5}");
                    }
#pragma warning restore 0618
                }
            }
        }

        private void DetectNewPositions()
        {
            var newly = _robot.Positions
                .Where(p => p.SymbolName == _robot.SymbolName
                    && (p.Label == "TP1Position" || p.Label == "TP2Position" || p.Label == "TP3Position")
                    && p.Quantity > 0
                    && !_mpos.Any(x => x.Id == p.Id))
                .ToList();

            foreach (var pos in newly)
            {
                if (!pos.StopLoss.HasValue) continue;
                var mp = new ManagedPosition
                {
                    Id = pos.Id,
                    TradeType = pos.TradeType,
                    EntryPrice = pos.EntryPrice,
                    InitialSL = pos.StopLoss.Value,
                    RDistanceInPips = Math.Abs(pos.EntryPrice - pos.StopLoss.Value) / _robot.Symbol.PipSize,
                    TrailingActivated = false
                };
                _mpos.Add(mp);
            }
        }
    }
}
