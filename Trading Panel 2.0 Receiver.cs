/*
 * ================================================================================
 * Trading Panel 2.0 - Receiver
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
using System.Net;
using System.Text;
using System.Text.Json;
using cAlgo;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    public enum TradeMode
    {
        Copy,
        Reverse
    }

    public enum SLInputMode
    {
        Pips,
        Price
    }

    public enum TPInputMode
    {
        Pips,
        Price
    }

    public enum SLCalculationMode
    {
        Pip_Dist,
        Price
    }

    public enum RiskCalculationMode
    {
        Percentage,
        Dollar_Amount
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

    public enum InstrumentSource
    {
        Broadcast,  // Use symbol from server broadcast
        Chart       // Always use chart symbol, ignore broadcast
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

        public static Style CreateStatusCardStyle()
        {
            var style = new Style();
            style.Set(ControlProperty.CornerRadius, 5);
            style.Set(ControlProperty.BackgroundColor, Color.FromArgb(217, 41, 41, 41), ControlState.DarkTheme);
            style.Set(ControlProperty.BackgroundColor, Color.FromArgb(230, 255, 255, 255), ControlState.LightTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#3C3C3C"), ControlState.DarkTheme);
            style.Set(ControlProperty.BorderColor, Color.FromHex("#C3C3C3"), ControlState.LightTheme);
            style.Set(ControlProperty.BorderThickness, new Thickness(1));
            style.Set(ControlProperty.Padding, new Thickness(0));
            return style;
        }
    }

    public class ReceiverStatusCard : CustomControl
    {
        private readonly NewsTradePanelWWebhook _bot;
        private readonly string _accountName;
        private readonly int _listeningPort;
        private readonly InstrumentSource _instrumentSource;
        private readonly TradeMode _tradeMode;
        private bool _portError;
        private string _currentInstrument;

        private Border _mainBorder;
        private StackPanel _contentPanel;

        private TextBlock _accountText;
        private TextBlock _instrumentSourceText;
        private TextBlock _instrumentText;
        private TextBlock _tradeModeText;
        private TextBlock _portText;
        private TextBlock _portStatusText;

        public ReceiverStatusCard(
            NewsTradePanelWWebhook bot,
            string accountName,
            int listeningPort,
            InstrumentSource instrumentSource,
            TradeMode tradeMode,
            string currentInstrument,
            bool portError)
        {
            _bot = bot;
            _accountName = accountName;
            _listeningPort = listeningPort;
            _instrumentSource = instrumentSource;
            _tradeMode = tradeMode;
            _currentInstrument = currentInstrument;
            _portError = portError;

            AddChild(CreateCard());
        }

        private ControlBase CreateCard()
        {
            _mainBorder = new Border
            {
                Style = Styles.CreateStatusCardStyle()
            };

            _contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(15, 8, 15, 8)
            };

            _accountText = new TextBlock
            {
                Text = $"Account: {_accountName}",
                FontSize = 9,
                Margin = "0 0 0 2"
            };
            _contentPanel.AddChild(_accountText);

            _tradeModeText = new TextBlock
            {
                Text = $"Trade Mode: {_tradeMode}",
                FontSize = 9,
                Margin = "0 2 0 2"
            };
            _contentPanel.AddChild(_tradeModeText);

            _instrumentSourceText = new TextBlock
            {
                Text = $"Instrument Source: {_instrumentSource}",
                FontSize = 9,
                Margin = "0 2 0 2"
            };
            _contentPanel.AddChild(_instrumentSourceText);

            _instrumentText = new TextBlock
            {
                Text = $"Instrument: {_currentInstrument}",
                FontSize = 9,
                Margin = "0 2 0 2"
            };
            _contentPanel.AddChild(_instrumentText);

            _portText = new TextBlock
            {
                Text = $"Listening Port: {_listeningPort}",
                FontSize = 9,
                Margin = "0 2 0 2"
            };
            _contentPanel.AddChild(_portText);

            _portStatusText = new TextBlock
            {
                Text = _portError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK",
                FontSize = 9,
                Margin = "0 2 0 0",
                ForegroundColor = _portError ? Color.Red : Color.Green
            };
            _contentPanel.AddChild(_portStatusText);

            _mainBorder.Child = _contentPanel;

            return _mainBorder;
        }

        public void UpdateStatus(string currentInstrument, bool portError)
        {
            _currentInstrument = currentInstrument;
            _portError = portError;

            _accountText.Text = $"Account: {_accountName}";
            _tradeModeText.Text = $"Trade Mode: {_tradeMode}";
            _instrumentSourceText.Text = $"Instrument Source: {_instrumentSource}";
            _instrumentText.Text = $"Instrument: {_currentInstrument}";
            _portText.Text = $"Listening Port: {_listeningPort}";
            _portStatusText.Text = _portError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK";
            _portStatusText.ForegroundColor = _portError ? Color.Red : Color.Green;
        }

        public void RefreshPortStatus(bool portError)
        {
            _portError = portError;
            _portStatusText.Text = _portError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK";
            _portStatusText.ForegroundColor = _portError ? Color.Red : Color.Green;
        }

        public void Show()
        {
            IsVisible = true;
        }

        public void Hide()
        {
            IsVisible = false;
        }
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class NewsTradePanelWWebhook : Robot
    {
        [Parameter("Account", DefaultValue = "FTMO 1")]
        public string AccountName { get; set; }

        [Parameter("Starting Balance", Group = "Risk Management", DefaultValue = 40000)]
        public double StartingBalance { get; set; }

        [Parameter("Risk Mode", Group = "Risk Management", DefaultValue = RiskCalculationMode.Percentage)]
        public RiskCalculationMode RiskMode { get; set; }

        [Parameter("Starting Risk (%)", Group = "Risk Management", DefaultValue = 1.0)]
        public double StartingRiskPercent { get; set; }

        [Parameter("Max Risk (%)", Group = "Risk Management", DefaultValue = 2.0)]
        public double MaxRiskPercent { get; set; }

        [Parameter("Starting Risk ($)", Group = "Risk Management", DefaultValue = 100.0)]
        public double StartingRiskDollar { get; set; }

        [Parameter("Max Risk ($)", Group = "Risk Management", DefaultValue = 200.0)]
        public double MaxRiskDollar { get; set; }

        [Parameter("Max Loss (%)", Group = "Risk Management", DefaultValue = 15.0)]
        public double MaxLossPercent { get; set; }

        [Parameter("Daily Stop Loss (%)", Group = "Risk Management", DefaultValue = 2.0)]
        public double DailyStopLossPercent { get; set; }

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

        [Parameter("Default SL Mode", Group = "Trade Parameters", DefaultValue = SLCalculationMode.Pip_Dist)]
        public SLCalculationMode DefaultSLMode { get; set; }

        [Parameter("Max Positive Trades/Day", Group = "Daily Limits", DefaultValue = 10)]
        public int MaxPositiveTradesDay { get; set; }

        [Parameter("Max Negative Trades/Day", Group = "Daily Limits", DefaultValue = 10)]
        public int MaxNegativeTradesDay { get; set; }

        [Parameter("Bot Log Folder", Group = "Logging",
            DefaultValue = @"C:\Users\juanc\iCloudDrive\Trading\US30 Terminator Bot\Bot Log")]
        public string LogFolder { get; set; }

        // ------------------------ "Receiver Parameters" ------------------------
        [Parameter("Listening Port", Group = "Receiver", DefaultValue = 8301)]
        public int ListeningPort { get; set; }

        [Parameter("Trade Mode", Group = "Receiver", DefaultValue = TradeMode.Copy)]
        public TradeMode TradeModeParam { get; set; }

        [Parameter("TP Modification Mode", Group = "Receiver", DefaultValue = TPModificationMode.Price)]
        public TPModificationMode TPModMode { get; set; }

        [Parameter("SL Modification Mode", Group = "Receiver", DefaultValue = SLModificationMode.Price)]
        public SLModificationMode SLModMode { get; set; }

        [Parameter("SL Input Mode", Group = "Receiver", DefaultValue = SLInputMode.Pips)]
        public SLInputMode SLInputModeParam { get; set; }

        [Parameter("TP Input Mode", Group = "Receiver", DefaultValue = TPInputMode.Price)]
        public TPInputMode TPInputModeParam { get; set; }

        [Parameter("SL Offset (pips)", Group = "Receiver", DefaultValue = 0.0)]
        public double SLOffsetPips { get; set; }

        [Parameter("Instrument Source", Group = "Receiver", DefaultValue = InstrumentSource.Broadcast)]
        public InstrumentSource InstrumentSourceMode { get; set; }
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
        private HttpListener _httpListener;
        private ReceiverStatusCard _statusCard;
        private bool _httpListenerError = false;
        private int _httpListenerStartAttempts = 0;
        private const int MAX_HTTP_LISTENER_ATTEMPTS = 3;

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

            if (RiskMode == RiskCalculationMode.Percentage)
            {
                PrintLocal($"Risk Mode: Percentage, Starting: {StartingRiskPercent}%, Max: {MaxRiskPercent}%");
            }
            else
            {
                PrintLocal($"Risk Mode: Dollar Amount, Starting: ${StartingRiskDollar}, Max: ${MaxRiskDollar}");
            }

            _dailyStartBalance = Account.Balance;
            _currentDayLocal = nowLocalEcu.Date;
            _manualPositionOpen = false;
            _manualTradeCumulativeProfit = 0.0;
            _dailyPosTrades = 0;
            _dailyNegTrades = 0;

            PrintLocal($"Receiver Mode: {TradeModeParam}, SL Input: {SLInputModeParam}, TP Input: {TPInputModeParam}, SL Offset: {SLOffsetPips} pips");
            PrintLocal($"Instrument Source: {InstrumentSourceMode}");
            if (InstrumentSourceMode == InstrumentSource.Chart)
                PrintLocal($"  All trades will execute on chart symbol: {SymbolName}");
            else
                PrintLocal($"  Trades will use broadcast symbol (fallback: {SymbolName})");
            PrintLocal($"Listening on port {ListeningPort}...");

            // Create and add TradingPanel UI
            var tradingPanel = new TradingPanel(
                this,
                Account,
                Symbol,
                DefaultStopLossPips,
                DefaultSLMode,
                RiskMode,
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

            // Start HTTP Listener for receiving trades
            TryStartHttpListener();

            // Create and display Status Card
            _statusCard = new ReceiverStatusCard(
                this,
                AccountName,
                ListeningPort,
                InstrumentSourceMode,
                TradeModeParam,
                SymbolName,
                _httpListenerError
            );

            var statusBorder = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = "0 20 20 0"
            };
            statusBorder.Child = _statusCard;
            Chart.AddControl(statusBorder);

            _statusCard.RefreshPortStatus(_httpListenerError);
        }

        protected override void OnTick()
        {
            // Retry HTTP listener start if needed
            if (_httpListenerError && _httpListenerStartAttempts > 0 && 
                _httpListenerStartAttempts < MAX_HTTP_LISTENER_ATTEMPTS)
            {
                TryStartHttpListener();
            }
            
            CheckDayChange();
            CheckHardStops();
            _tradeManager.CheckPositions();
        }

        protected override void OnStop()
        {
            try
            {
                if (_httpListener != null)
                {
                    try
                    {
                        if (_httpListener.IsListening)
                        {
                            _httpListener.Stop();
                        }
                        _httpListener.Close();
                        ((IDisposable)_httpListener).Dispose();
                        PrintLocal("HTTP Listener stopped");
                    }
                    catch (Exception ex)
                    {
                        PrintLocal($"HTTP Listener error during shutdown: {ex.Message}");
                    }
                    finally
                    {
                        _httpListener = null;
                    }
                }
            }
            catch (Exception ex)
            {
                PrintLocal($"Critical error in OnStop: {ex.Message}");
            }
            
            WriteLogsToFile();
            PrintLocal("Receiver stopped.");
        }

        // -----------------------------------------------------------------------
        // HTTP Listener Start with Retry Logic
        // -----------------------------------------------------------------------
        private void TryStartHttpListener()
        {
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{ListeningPort}/newtrade/");
                _httpListener.Start();
                _httpListener.BeginGetContext(ListenerCallback, _httpListener);
                _httpListenerError = false;
                _httpListenerStartAttempts = 0;
                PrintLocal($"HTTP Listener started successfully on port {ListeningPort}");
                
                if (_statusCard != null)
                {
                    _statusCard.RefreshPortStatus(_httpListenerError);
                }
            }
            catch (HttpListenerException ex)
            {
                _httpListenerError = true;
                _httpListenerStartAttempts++;
                
                if (_httpListenerStartAttempts < MAX_HTTP_LISTENER_ATTEMPTS)
                {
                    PrintLocal($"Port {ListeningPort} in use, will retry (attempt {_httpListenerStartAttempts}/{MAX_HTTP_LISTENER_ATTEMPTS})");
                }
                else
                {
                    PrintLocal($"Failed after {MAX_HTTP_LISTENER_ATTEMPTS} attempts: {ex.Message}");
                }
                
                if (_statusCard != null)
                {
                    _statusCard.RefreshPortStatus(_httpListenerError);
                }
            }
            catch (Exception ex)
            {
                _httpListenerError = true;
                _httpListenerStartAttempts = MAX_HTTP_LISTENER_ATTEMPTS;
                PrintLocal($"Failed to start HTTP Listener: {ex.Message}");
                
                if (_statusCard != null)
                {
                    _statusCard.RefreshPortStatus(_httpListenerError);
                }
            }
        }

        // -----------------------------------------------------------------------
        // HTTP Listener Callback - Receives incoming trade messages
        // -----------------------------------------------------------------------
        private void ListenerCallback(IAsyncResult ar)
        {
            if (_httpListener == null || !_httpListener.IsListening)
                return;

            try
            {
                HttpListenerContext ctx = _httpListener.EndGetContext(ar);
                _httpListener.BeginGetContext(ListenerCallback, _httpListener);

                string body = ReadRequestBody(ctx);
                BeginInvokeOnMainThread(() => ProcessIncomingTrade(body));

                // Send response
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                PrintLocal($"ListenerCallback error: {ex.Message}");
            }
        }

        private string ReadRequestBody(HttpListenerContext ctx)
        {
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        // -----------------------------------------------------------------------
        // Process Incoming Trade - Parse JSON and execute trade
        // -----------------------------------------------------------------------
        private void ProcessIncomingTrade(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    PrintLocal("Received empty JSON message");
                    return;
                }

                var message = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (message == null || !message.ContainsKey("action"))
                {
                    PrintLocal("Invalid JSON: missing 'action' field");
                    return;
                }

                string action = message["action"].GetString();
                
                // Determine which symbol to use based on InstrumentSourceMode setting
                string symbol;
                if (InstrumentSourceMode == InstrumentSource.Chart)
                {
                    // Always use chart symbol, ignore broadcast
                    symbol = SymbolName;
                    if (message.ContainsKey("symbol"))
                    {
                        string broadcastSymbol = message["symbol"].GetString();
                        PrintLocal($"Received: {action} for {broadcastSymbol} -> Using chart instrument: {symbol}");
                    }
                    else
                    {
                        PrintLocal($"Received: {action} for {symbol}");
                    }
                }
                else
                {
                    // Use broadcast symbol if provided, otherwise fallback to chart
                    symbol = message.ContainsKey("symbol") ? message["symbol"].GetString() : SymbolName;
                    PrintLocal($"Received: {action} for {symbol}");
                }

                // Handle ModifyTP action
                if (action == "ModifyTP")
                {
                    ProcessTPModification(message);
                    return;
                }

                // Handle ModifySL action
                if (action == "ModifySL")
                {
                    ProcessSLModification(message);
                    return;
                }

                // Handle ClosePosition action (individual position closure)
                if (action == "ClosePosition")
                {
                    ProcessPositionClosure(message);
                    return;
                }

                // Handle Close All Positions
                if (action == "Close All Positions")
                {
                    CloseAllPositions();
                    return;
                }

                // Handle Buy/Sell trades
                if (action == "Buy" || action == "Sell")
                {
                    TradeType receivedType = (action == "Buy") ? TradeType.Buy : TradeType.Sell;

                    // Extract SL data (either pips or price)
                    double? slPips = message.ContainsKey("sl_pips") ? (double?)message["sl_pips"].GetDouble() : null;
                    double? slPrice = message.ContainsKey("sl_price") ? (double?)message["sl_price"].GetDouble() : null;

                    if (!slPips.HasValue && !slPrice.HasValue)
                    {
                        PrintLocal("Trade rejected: No SL data (sl_pips or sl_price) provided");
                        return;
                    }

                    // Extract TP levels if present
                    JsonElement? tpLevels = message.ContainsKey("tp_levels") ? (JsonElement?)message["tp_levels"] : null;

                    ExecuteReceivedTrade(receivedType, slPips, slPrice, tpLevels, symbol);
                }
                else
                {
                    PrintLocal($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                PrintLocal($"ProcessIncomingTrade error: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------------
        // Execute Received Trade - Full implementation with Copy/Reverse mode
        // -----------------------------------------------------------------------
        private void ExecuteReceivedTrade(TradeType receivedType, double? slPips, double? slPrice, JsonElement? tpLevelsElement, string symbol)
        {
            if (!CanTrade())
            {
                PrintLocal("Received trade blocked => equity below threshold or stopTrading=true");
                return;
            }

            // 1. Determine actual trade direction based on TradeMode (Copy or Reverse)
            TradeType actualType = (TradeModeParam == TradeMode.Copy) 
                ? receivedType 
                : (receivedType == TradeType.Buy ? TradeType.Sell : TradeType.Buy);

            PrintLocal($"Received: {receivedType}, Mode: {TradeModeParam}, Executing: {actualType}");

            // 2. Calculate SL distance in pips - based on SL Input Mode setting
            double finalSlPips = 0;

            if (SLInputModeParam == SLInputMode.Pips)
            {
                // Prefer pips format
                if (slPips.HasValue)
                {
                    finalSlPips = slPips.Value;
                    PrintLocal($"SL received as pips: {slPips.Value} pips");
                }
                else if (slPrice.HasValue)
                {
                    // Fallback to price if pips not available
                    double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                    double priceDiff = Math.Abs(currentPrice - slPrice.Value);
                    finalSlPips = priceDiff / Symbol.PipSize;
                    PrintLocal($"SL received as price (fallback): {slPrice.Value}, Current: {currentPrice}, Calculated: {finalSlPips:F1} pips");
                }
                else
                {
                    PrintLocal($"SL calculation failed: No sl_pips or sl_price provided");
                    return;
                }
            }
            else // SLInputMode.Price
            {
                // Prefer price format
                if (slPrice.HasValue)
                {
                    double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                    double priceDiff = Math.Abs(currentPrice - slPrice.Value);
                    finalSlPips = priceDiff / Symbol.PipSize;
                    PrintLocal($"SL received as price: {slPrice.Value}, Current: {currentPrice}, Calculated: {finalSlPips:F1} pips");
                }
                else if (slPips.HasValue)
                {
                    // Fallback to pips if price not available
                    finalSlPips = slPips.Value;
                    PrintLocal($"SL received as pips (fallback): {slPips.Value} pips");
                }
                else
                {
                    PrintLocal($"SL calculation failed: No sl_pips or sl_price provided");
                    return;
                }
            }

            // 3. Apply SL Offset
            if (SLOffsetPips != 0)
            {
                double originalSL = finalSlPips;
                finalSlPips += SLOffsetPips;
                PrintLocal($"SL Offset applied: {originalSL:F1} + {SLOffsetPips:F1} = {finalSlPips:F1} pips");
            }

            if (finalSlPips <= 0)
            {
                PrintLocal($"Invalid SL distance: {finalSlPips:F1} pips");
                return;
            }

            // 4. Calculate position size using receiver's own risk settings
            double riskAmt = GetDynamicRiskAmount();
            if (riskAmt <= 0)
            {
                PrintLocal("Dynamic risk amount is 0 or negative");
                return;
            }

            double volumeUnits = riskAmt / (finalSlPips * Symbol.PipValue);
            volumeUnits = Symbol.NormalizeVolumeInUnits(volumeUnits, RoundingMode.ToNearest);
            
            if (volumeUnits < Symbol.VolumeInUnitsMin)
                volumeUnits = Symbol.VolumeInUnitsMin;

            PrintLocal($"Risk Amount: ${riskAmt:F2}, Total Volume: {volumeUnits} units");

            // 5. Parse TP levels from broadcast first to determine how many positions to open
            double tp1Pips = 0, tp2Pips = 0, tp3Pips = 0;
            double tp1Price = 0, tp2Price = 0, tp3Price = 0;
            bool hasTPLevels = false;
            int tpCount = 0;

            if (tpLevelsElement.HasValue)
            {
                try
                {
                    var tpArray = tpLevelsElement.Value.EnumerateArray().ToList();
                    tpCount = tpArray.Count;
                    
                    if (tpCount >= 2)
                    {
                        // Parse TP1 based on TPInputMode
                        if (TPInputModeParam == TPInputMode.Price)
                        {
                            // Prioritize price
                            if (tpArray[0].TryGetProperty("tp_price", out var tp1PriceElement))
                            {
                                tp1Price = tp1PriceElement.GetDouble();
                                // Calculate pips from price
                                double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                tp1Pips = Math.Abs(tp1Price - currentPrice) / Symbol.PipSize;
                            }
                            else if (tpArray[0].TryGetProperty("tp_pips", out var tp1PipsElement))
                            {
                                // Fallback to pips
                                tp1Pips = tp1PipsElement.GetDouble();
                                tp1Price = CalculateTPFromPips(actualType, Symbol.Ask, tp1Pips);
                            }
                        }
                        else // TPInputMode.Pips
                        {
                            // Prioritize pips
                            if (tpArray[0].TryGetProperty("tp_pips", out var tp1PipsElement))
                            {
                                tp1Pips = tp1PipsElement.GetDouble();
                                tp1Price = CalculateTPFromPips(actualType, Symbol.Ask, tp1Pips);
                            }
                            else if (tpArray[0].TryGetProperty("tp_price", out var tp1PriceElement))
                            {
                                // Fallback to price
                                tp1Price = tp1PriceElement.GetDouble();
                                double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                tp1Pips = Math.Abs(tp1Price - currentPrice) / Symbol.PipSize;
                            }
                        }
                        
                        // Parse TP2 based on TPInputMode
                        if (TPInputModeParam == TPInputMode.Price)
                        {
                            if (tpArray[1].TryGetProperty("tp_price", out var tp2PriceElement))
                            {
                                tp2Price = tp2PriceElement.GetDouble();
                                double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                tp2Pips = Math.Abs(tp2Price - currentPrice) / Symbol.PipSize;
                            }
                            else if (tpArray[1].TryGetProperty("tp_pips", out var tp2PipsElement))
                            {
                                tp2Pips = tp2PipsElement.GetDouble();
                                tp2Price = CalculateTPFromPips(actualType, Symbol.Ask, tp2Pips);
                            }
                        }
                        else // TPInputMode.Pips
                        {
                            if (tpArray[1].TryGetProperty("tp_pips", out var tp2PipsElement))
                            {
                                tp2Pips = tp2PipsElement.GetDouble();
                                tp2Price = CalculateTPFromPips(actualType, Symbol.Ask, tp2Pips);
                            }
                            else if (tpArray[1].TryGetProperty("tp_price", out var tp2PriceElement))
                            {
                                tp2Price = tp2PriceElement.GetDouble();
                                double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                tp2Pips = Math.Abs(tp2Price - currentPrice) / Symbol.PipSize;
                            }
                        }
                        
                        // Parse TP3 if present
                        if (tpCount >= 3)
                        {
                            if (TPInputModeParam == TPInputMode.Price)
                            {
                                if (tpArray[2].TryGetProperty("tp_price", out var tp3PriceElement))
                                {
                                    tp3Price = tp3PriceElement.GetDouble();
                                    double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                    tp3Pips = Math.Abs(tp3Price - currentPrice) / Symbol.PipSize;
                                }
                                else if (tpArray[2].TryGetProperty("tp_pips", out var tp3PipsElement))
                                {
                                    tp3Pips = tp3PipsElement.GetDouble();
                                    tp3Price = CalculateTPFromPips(actualType, Symbol.Ask, tp3Pips);
                                }
                            }
                            else // TPInputMode.Pips
                            {
                                if (tpArray[2].TryGetProperty("tp_pips", out var tp3PipsElement))
                                {
                                    tp3Pips = tp3PipsElement.GetDouble();
                                    tp3Price = CalculateTPFromPips(actualType, Symbol.Ask, tp3Pips);
                                }
                                else if (tpArray[2].TryGetProperty("tp_price", out var tp3PriceElement))
                                {
                                    tp3Price = tp3PriceElement.GetDouble();
                                    double currentPrice = (actualType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
                                    tp3Pips = Math.Abs(tp3Price - currentPrice) / Symbol.PipSize;
                                }
                            }
                            PrintLocal($"TP Levels received: TP1={tp1Price:F5}, TP2={tp2Price:F5}, TP3={tp3Price:F5}");
                        }
                        else
                        {
                            PrintLocal($"TP Levels received: TP1={tp1Price:F5}, TP2={tp2Price:F5} (only 2 TPs)");
                        }
                        
                        hasTPLevels = true;
                    }
                }
                catch (Exception ex)
                {
                    PrintLocal($"Error parsing TP levels: {ex.Message}");
                }
            }

            // 6. Split into positions based on how many TPs were received
            double pos1 = 0, pos2 = 0, pos3 = 0;
            bool hasTP3 = (hasTPLevels && tpCount >= 3);
            
            if (hasTP3)
            {
                // 3 TP levels - use normal split
                pos1 = volumeUnits * (TP1Percent / 100.0);
                pos2 = volumeUnits * (TP2Percent / 100.0);
                pos3 = volumeUnits * (TP3Percent / 100.0);
            }
            else if (hasTPLevels && tpCount == 2)
            {
                // Only 2 TP levels - redistribute volume between TP1 and TP2
                double total12 = TP1Percent + TP2Percent;
                pos1 = volumeUnits * (TP1Percent / total12);
                pos2 = volumeUnits * (TP2Percent / total12);
                pos3 = 0;
                PrintLocal($"Redistributing volume for 2 TPs: {TP1Percent}/{total12} and {TP2Percent}/{total12}");
            }
            else
            {
                // No TP levels received - use all 3 positions with default settings
                pos1 = volumeUnits * (TP1Percent / 100.0);
                pos2 = volumeUnits * (TP2Percent / 100.0);
                pos3 = volumeUnits * (TP3Percent / 100.0);
            }

            pos1 = Symbol.NormalizeVolumeInUnits(pos1, RoundingMode.ToNearest);
            pos2 = Symbol.NormalizeVolumeInUnits(pos2, RoundingMode.ToNearest);
            if (hasTP3 || !hasTPLevels)
                pos3 = Symbol.NormalizeVolumeInUnits(pos3, RoundingMode.ToNearest);

            if (pos1 < Symbol.VolumeInUnitsMin) pos1 = Symbol.VolumeInUnitsMin;
            if (pos2 < Symbol.VolumeInUnitsMin) pos2 = Symbol.VolumeInUnitsMin;
            if ((hasTP3 || !hasTPLevels) && pos3 < Symbol.VolumeInUnitsMin) pos3 = Symbol.VolumeInUnitsMin;

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
                    // Calculate TP from pip distance (automatically correct for direction)
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

            // 8. Execute market orders with calculated TP prices
            if (hasTP3)
            {
                PrintLocal($"Executing 3 positions: TP1={pos1} units, TP2={pos2}, TP3={pos3}");
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos1, "TP1Position", finalSlPips, finalTP1);
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos2, "TP2Position", finalSlPips, finalTP2);
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos3, "TP3Position", finalSlPips, finalTP3);
            }
            else if (hasTPLevels && tpCount == 2)
            {
                PrintLocal($"Executing 2 positions: TP1={pos1} units, TP2={pos2}");
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos1, "TP1Position", finalSlPips, finalTP1);
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos2, "TP2Position", finalSlPips, finalTP2);
            }
            else
            {
                PrintLocal($"Executing 3 positions (default): TP1={pos1} units, TP2={pos2}, TP3={pos3}");
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos1, "TP1Position", finalSlPips, finalTP1);
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos2, "TP2Position", finalSlPips, finalTP2);
                ExecuteMarketOrderWithTPPrice(actualType, symbol, pos3, "TP3Position", finalSlPips, finalTP3);
            }

            // 8. Set position tracking flag
            _manualPositionOpen = true;
            _manualTradeCumulativeProfit = 0;

            PrintLocal($"Received trade executed: {actualType}, SL={finalSlPips:F1} pips");
        }

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

        private void ExecuteMarketOrderWithTPPrice(TradeType tradeType, string symbol, double volume, string label, double slPips, double tpPrice)
        {
            // Get current entry price
            double entryPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
            
            // Calculate TP distance in price units
            double tpPriceDistance = Math.Abs(tpPrice - entryPrice);
            
            // Convert to pips (API expects pips, not price distance - works for all instruments)
            double tpPips = tpPriceDistance / Symbol.PipSize;
            
#pragma warning disable 0618
            var result = ExecuteMarketOrder(tradeType, symbol, volume, label, slPips, tpPips);
#pragma warning restore 0618
            
            if (result.IsSuccessful)
            {
                PrintLocal($"Position opened: {label}, Volume={volume}, SL={slPips:F1}p, TP={tpPrice:F5} ({tpPips:F1}p)");
            }
            else
            {
                PrintLocal($"Failed to open {label}: {result.Error}");
            }
        }

        private void ProcessTPModification(Dictionary<string, JsonElement> message)
        {
            try
            {
                string posLabel = message["position_label"].GetString();
                string tradeTypeStr = message["trade_type"].GetString();
                double tpPrice = message["tp_price"].GetDouble();
                double tpPipDiff = message["tp_pip_diff"].GetDouble();
                
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

        private void ProcessSLModification(Dictionary<string, JsonElement> message)
        {
            try
            {
                string posLabel = message["position_label"].GetString();
                string tradeTypeStr = message["trade_type"].GetString();
                double slPrice = message["sl_price"].GetDouble();
                double slPipDiff = message["sl_pip_diff"].GetDouble();
                
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
                    PrintLocal($"Closing position: {pos.Id} ({pos.Label})");
                }
            }
            catch (Exception ex)
            {
                PrintLocal($"Error processing position closure: {ex.Message}");
            }
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
        // Execute Manual Trade - Called from TradingPanel UI
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
            double riskAmt = GetDynamicRiskAmount();
            if (riskAmt <= 0) return;

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

            _manualPositionOpen = true;
            _manualTradeCumulativeProfit = 0;
        }



        public void CloseAllPositions()
        {
            foreach (var pos in Positions)
                if (pos.SymbolName == SymbolName)
                    ClosePositionAsync(pos);
            
            PrintLocal("All positions closed via received command");
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
                }
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



        public double GetDynamicRiskAmount()
        {
            if (RiskMode == RiskCalculationMode.Percentage)
            {
                // Percentage mode - calculate dynamic risk percent, then convert to dollar amount
                double riskPercent = CalculateDynamicRiskPercent();
                return Account.Balance * (riskPercent / 100.0);
            }
            else
            {
                // Dollar Amount mode - scale dollar amount based on profit/loss
                double riskDollar = CalculateDynamicRiskDollar();
                return riskDollar;
            }
        }

        private double CalculateDynamicRiskPercent()
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

        private double CalculateDynamicRiskDollar()
        {
            double bal = Account.Balance;
            double diff = bal - StartingBalance;
            double diffPercent = (diff / StartingBalance) * 100.0;
            double R = StartingRiskDollar;
            double newRisk = R;

            if (diffPercent < 0)
            {
                // Scale down on losses
                if (diffPercent >= -5.0)
                    newRisk = R;
                else if (diffPercent >= -7.5)
                    newRisk = R / 2.0;
                else
                    newRisk = R / 4.0;
            }
            else
            {
                // Scale up on profits
                double incrementsNeeded = Math.Floor(diffPercent / 2.0);
                double extraRisk = incrementsNeeded * (0.2 * R);
                newRisk = R + extraRisk;
            }

            if (newRisk > MaxRiskDollar)
                newRisk = MaxRiskDollar;

            return newRisk;
        }

        public double GetDynamicRiskPercent()
        {
            // Legacy method for backward compatibility - returns percentage
            if (RiskMode == RiskCalculationMode.Percentage)
            {
                return CalculateDynamicRiskPercent();
            }
            else
            {
                // Convert dollar amount to percentage for display
                double riskDollar = CalculateDynamicRiskDollar();
                return (riskDollar / Account.Balance) * 100.0;
            }
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
        private Button riskPercentModeButton;
        private Button riskDollarModeButton;
        private TradeType selectedTradeType = TradeType.Buy;
        private SLCalculationMode selectedSLMode;
        private RiskCalculationMode selectedRiskMode;

        public TradingPanel(NewsTradePanelWWebhook bot,
                            IAccount account,
                            Symbol symbol,
                            double defaultStopLossPips,
                            SLCalculationMode defaultSLMode,
                            RiskCalculationMode defaultRiskMode,
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
            selectedRiskMode = defaultRiskMode;

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
                Text = "SkyTrading Panel Receiver",
                FontSize = 9,
                Margin = "0 8 0 0",
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.7
            };
            modeStack.AddChild(brandingText);
            
            main.AddChild(modeStack);

            var riskModeStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 0 10 0"
            };
            var riskModeLabel = new TextBlock { Text = "Risk Mode" };
            riskModeStack.AddChild(riskModeLabel);

            var riskModeButtonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = "0 5 0 0"
            };
            riskPercentModeButton = new Button
            {
                Text = "%",
                Width = 50,
                Margin = "0 0 5 0"
            };
            riskPercentModeButton.Click += (args) => SelectRiskMode(RiskCalculationMode.Percentage);
            riskModeButtonStack.AddChild(riskPercentModeButton);

            riskDollarModeButton = new Button
            {
                Text = "$",
                Width = 50
            };
            riskDollarModeButton.Click += (args) => SelectRiskMode(RiskCalculationMode.Dollar_Amount);
            riskModeButtonStack.AddChild(riskDollarModeButton);

            riskModeStack.AddChild(riskModeButtonStack);
            main.AddChild(riskModeStack);

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
            SelectRiskMode(selectedRiskMode);
            UpdateValues();
            return main;
        }

        private void SelectRiskMode(RiskCalculationMode mode)
        {
            selectedRiskMode = mode;
            
            if (mode == RiskCalculationMode.Percentage)
            {
                ApplyButtonStyle(riskPercentModeButton, true, Color.FromHex("#666666"));
                ApplyButtonStyle(riskDollarModeButton, false, Color.FromHex("#666666"));
            }
            else
            {
                ApplyButtonStyle(riskPercentModeButton, false, Color.FromHex("#666666"));
                ApplyButtonStyle(riskDollarModeButton, true, Color.FromHex("#666666"));
            }
            
            UpdateValues();
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

            double riskAmt = _bot.CanTrade() ? _bot.GetDynamicRiskAmount() : 0.0;

            double volumeUnits = (slPips > 0) ? riskAmt / (slPips * _symbol.PipValue) : 0;
            volumeUnits = _symbol.NormalizeVolumeInUnits(volumeUnits, RoundingMode.ToNearest);
            if (volumeUnits < _symbol.VolumeInUnitsMin)
                volumeUnits = _symbol.VolumeInUnitsMin;

            double lots = volumeUnits / _symbol.LotSize;
            
            // Display based on selected risk mode
            if (selectedRiskMode == RiskCalculationMode.Percentage)
            {
                double riskPercent = (riskAmt / _account.Balance) * 100.0;
                estimatedRiskTextBlock.Text = $"Risk: ${riskAmt:F2}";
                riskPercentTextBlock.Text = $"Risk: {riskPercent:F2}%";
            }
            else
            {
                estimatedRiskTextBlock.Text = $"Risk: ${riskAmt:F2}";
                riskPercentTextBlock.Text = $"Fixed: ${riskAmt:F2}";
            }
            
            qtyTextBlock.Text = $"Qty: {lots:F2}";
        }

        private void ExecuteManual()
        {
            double slPips = GetVal(slTextBox, 20);
            _bot.ExecuteManualTrade(
                selectedTradeType,
                slPips,
                TP1Percent,
                TP2Percent,
                TP3Percent,
                TP1R,
                TP2R,
                TP3R
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
