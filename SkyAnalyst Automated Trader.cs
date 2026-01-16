/*
 * ================================================================================
 * SkyAnalyst Automated Trader
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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public enum SymbolFilterOption
    {
        US30_Pepperstone,
        US500_Pepperstone,
        NAS100_Pepperstone,
        XAUUSD_Pepperstone,
        XAGUSD_Pepperstone,
        EURUSD,
        GBPUSD,
        USDJPY,
        AUDUSD,
        USDCAD,
        NZDUSD,
        USDCHF,
        EURGBP,
        EURJPY,
        GBPJPY,
        BTCUSD,
        US30_CASH_FTMO,
        US500_CASH_FTMO,
        US100_CASH_FTMO,
        Accept_Any
    }

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

    public enum BotModeType
    {
        Auto,
        Manual_Only
    }

    public enum StatusCardVisibility
    {
        Displayed,
        Hidden
    }

    public class PriceZone
    {
        [JsonPropertyName("min")]
        public double Min { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }

        [JsonPropertyName("mid")]
        public double Mid { get; set; }

        [JsonPropertyName("is_zone")]
        public bool IsZone { get; set; }

        [JsonPropertyName("aggressive")]
        public double Aggressive { get; set; }

        [JsonPropertyName("conservative")]
        public double Conservative { get; set; }

        [JsonPropertyName("tight")]
        public double Tight { get; set; }

        [JsonPropertyName("wide")]
        public double Wide { get; set; }

        [JsonPropertyName("early")]
        public double Early { get; set; }

        [JsonPropertyName("full")]
        public double Full { get; set; }
    }

    public class WebhookData
    {
        [JsonPropertyName("alert_type")]
        public string AlertType { get; set; }

        [JsonPropertyName("alert_id")]
        public string AlertId { get; set; }

        [JsonPropertyName("trade_id")]
        public string TradeId { get; set; }

        [JsonPropertyName("instrument")]
        public string Instrument { get; set; }

        [JsonPropertyName("direction")]
        public string Direction { get; set; }

        [JsonPropertyName("time")]
        public string Time { get; set; }

        [JsonPropertyName("entry_zone")]
        public PriceZone EntryZone { get; set; }

        [JsonPropertyName("stop_loss")]
        public PriceZone StopLoss { get; set; }

        [JsonPropertyName("tp1")]
        public PriceZone TP1 { get; set; }

        [JsonPropertyName("tp2")]
        public PriceZone TP2 { get; set; }

        [JsonPropertyName("tp3")]
        public PriceZone TP3 { get; set; }

        [JsonPropertyName("ai_decision")]
        public string AiDecision { get; set; }

        [JsonPropertyName("confidence")]
        public int? Confidence { get; set; }
    }

    public class WebhookPayloadWrapper
    {
        [JsonPropertyName("Data")]
        public WebhookData Data { get; set; }
    }

    public class WebhookPayload : WebhookData
    {
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

    public class StatusCard : CustomControl
    {
        private readonly NewsTradePanelWWebhook _bot;
        private readonly string _accountName;
        private readonly RiskModeType _riskMode;
        private readonly int _webhookPort;
        private BotModeType _botMode;
        private bool _webhookServerError;

        private Border _mainBorder;
        private StackPanel _contentPanel;

        private TextBlock _accountText;
        private TextBlock _riskModeText;
        private TextBlock _portText;
        private TextBlock _portStatusText;
        private TextBlock _botModeText;

        public StatusCard(
            NewsTradePanelWWebhook bot,
            string accountName,
            RiskModeType riskMode,
            int webhookPort,
            BotModeType botMode,
            bool webhookServerError)
        {
            _bot = bot;
            _accountName = accountName;
            _riskMode = riskMode;
            _webhookPort = webhookPort;
            _botMode = botMode;
            _webhookServerError = webhookServerError;

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

            _riskModeText = new TextBlock
            {
                Text = $"Risk Mode: {_riskMode}",
                FontSize = 9,
                Margin = "0 2 0 2"
            };
            _contentPanel.AddChild(_riskModeText);

            _portText = new TextBlock
            {
                Text = $"Listening Port: {_webhookPort}",
                FontSize = 9,
                Margin = "0 2 0 2",
                IsVisible = _botMode == BotModeType.Auto
            };
            _contentPanel.AddChild(_portText);

            _portStatusText = new TextBlock
            {
                Text = _webhookServerError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK",
                FontSize = 9,
                Margin = "0 2 0 2",
                ForegroundColor = _webhookServerError ? Color.Red : Color.Green,
                IsVisible = _botMode == BotModeType.Auto
            };
            _contentPanel.AddChild(_portStatusText);

            _botModeText = new TextBlock
            {
                Text = $"Bot Mode: {_botMode}",
                FontSize = 9,
                Margin = "0 2 0 0"
            };
            _contentPanel.AddChild(_botModeText);

            _mainBorder.Child = _contentPanel;

            return _mainBorder;
        }

        public void UpdateStatus(BotModeType botMode, bool webhookServerError)
        {
            _botMode = botMode;
            _webhookServerError = webhookServerError;

            _accountText.Text = $"Account: {_accountName}";
            _riskModeText.Text = $"Risk Mode: {_riskMode}";
            _botModeText.Text = $"Bot Mode: {_botMode}";

            if (_botMode == BotModeType.Auto)
            {
                _portText.IsVisible = true;
                _portStatusText.IsVisible = true;
                _portText.Text = $"Listening Port: {_webhookPort}";
                _portStatusText.Text = _webhookServerError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK";
                _portStatusText.ForegroundColor = _webhookServerError ? Color.Red : Color.Green;
            }
            else
            {
                _portText.IsVisible = false;
                _portStatusText.IsVisible = false;
            }
        }

        public void RefreshPortStatus(bool webhookServerError)
        {
            _webhookServerError = webhookServerError;
            _portStatusText.Text = _webhookServerError ? "Port Status: ⚠️ Error" : "Port Status: ✓ OK";
            _portStatusText.ForegroundColor = _webhookServerError ? Color.Red : Color.Green;
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
        [Parameter("Bot Mode", DefaultValue = BotModeType.Auto)]
        public BotModeType BotMode { get; set; }

        [Parameter("Status Card", DefaultValue = StatusCardVisibility.Displayed)]
        public StatusCardVisibility StatusCardVisibility { get; set; }

        [Parameter("Account", DefaultValue = "Demo Account")]
        public string AccountName { get; set; }

        // ------------------------ "Risk Mode" ------------------------
        [Parameter("Risk Mode", Group = "Risk Mode", DefaultValue = RiskModeType.Fixed)]
        public RiskModeType RiskMode { get; set; }

        // ------------------------ "Fixed Risk Settings" ------------------------
        [Parameter("Fixed Risk Unit", Group = "Fixed Risk Settings", DefaultValue = FixedRiskUnit.Percent)]
        public FixedRiskUnit FixedRiskUnit { get; set; }

        [Parameter("Fixed Risk (%)", Group = "Fixed Risk Settings", DefaultValue = 1.0)]
        public double FixedRiskPercent { get; set; }

        [Parameter("Fixed Risk ($)", Group = "Fixed Risk Settings", DefaultValue = 100.0)]
        public double FixedRiskDollar { get; set; }

        // ------------------------ "Dynamic Risk Settings" ------------------------
        [Parameter("Starting Balance", Group = "Dynamic Risk Settings", DefaultValue = 1000)]
        public double StartingBalance { get; set; }

        [Parameter("Base Risk (%)", Group = "Dynamic Risk Settings", DefaultValue = 1.0)]
        public double BaseRiskPercent { get; set; }

        [Parameter("Max Risk (%)", Group = "Dynamic Risk Settings", DefaultValue = 2.0)]
        public double MaxRiskPercent { get; set; }

        // ------------------------ "Risk Scaling on Growth" ------------------------
        [Parameter("Equity Growth Step (%)", Group = "Risk Scaling on Growth", DefaultValue = 3.0)]
        public double EquityGrowthStep { get; set; }

        [Parameter("Risk Increase per Step (%)", Group = "Risk Scaling on Growth", DefaultValue = 20.0)]
        public double RiskIncreasePerStep { get; set; }

        [Parameter("Risk Compounding", Group = "Risk Scaling on Growth", DefaultValue = true)]
        public bool RiskCompounding { get; set; }

        // ------------------------ "Risk Reduction on Drawdown" ------------------------
        [Parameter("Drawdown Step (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 5.0)]
        public double DrawdownStep { get; set; }

        [Parameter("Risk Reduction per Step (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 50.0)]
        public double RiskReductionPerStep { get; set; }

        [Parameter("Max Drawdown (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 15.0)]
        public double MaxDrawdown { get; set; }

        [Parameter("Max Daily Loss (%)", Group = "Risk Reduction on Drawdown", DefaultValue = 5.0)]
        public double MaxDailyLoss { get; set; }

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

        // ------------------------ "Broadcast Trade Settings" ------------------------
        [Parameter("Ports Active", Group = "Broadcast Trade Settings", DefaultValue = 0, MinValue = 0, MaxValue = 4)]
        public int PortsActive { get; set; }

        [Parameter("Port 1", Group = "Broadcast Trade Settings", DefaultValue = 8301)]
        public int Port1 { get; set; }

        [Parameter("Port 2", Group = "Broadcast Trade Settings", DefaultValue = 8302)]
        public int Port2 { get; set; }

        [Parameter("Port 3", Group = "Broadcast Trade Settings", DefaultValue = 8303)]
        public int Port3 { get; set; }

        [Parameter("Port 4", Group = "Broadcast Trade Settings", DefaultValue = 8304)]
        public int Port4 { get; set; }

        [Parameter("JSON SL Format", Group = "Broadcast Trade Settings", DefaultValue = JSONSLFormat.Pips)]
        public JSONSLFormat JsonSLFormat { get; set; }
        // -----------------------------------------------------------------------

        // ------------------------ "Webhook Settings" ------------------------
        [Parameter("Webhook Port", Group = "Webhook Settings", DefaultValue = 8050)]
        public int WebhookPort { get; set; }

        [Parameter("Webhook Enabled", Group = "Webhook Settings", DefaultValue = true)]
        public bool WebhookEnabled { get; set; }

        [Parameter("Broker Match Validation", Group = "Webhook Settings", DefaultValue = true)]
        public bool BrokerMatchValidation { get; set; }

        [Parameter("Expected Broker Name", Group = "Webhook Settings", DefaultValue = "Pepperstone")]
        public string ExpectedBrokerName { get; set; }

        [Parameter("Symbol Filter", Group = "Webhook Settings", DefaultValue = SymbolFilterOption.US30_Pepperstone)]
        public SymbolFilterOption SymbolFilter { get; set; }

        [Parameter("SL Zone Preference", Group = "Webhook Settings", DefaultValue = SLZonePreference.Wide)]
        public SLZonePreference SLZonePreference { get; set; }

        [Parameter("TP Zone Preference", Group = "Webhook Settings", DefaultValue = TPZonePreference.Full)]
        public TPZonePreference TPZonePreference { get; set; }
        // -----------------------------------------------------------------------

        // ------------------------ "Logging" ------------------------
        [Parameter("Bot Log Folder", Group = "Logging",
            DefaultValue = @"C:\Users\juanc\iCloudDrive\Trading\US30 Terminator Bot\Bot Log")]
        public string LogFolder { get; set; }

        // ------------------------ "⚠️ For Educational Purposes Only" ------------------------
        [Parameter("Disclaimer", Group = "⚠️ For Educational Purposes Only", DefaultValue = "This bot is for educational and testing purposes only. Use at your own risk.")]
        public string Disclaimer { get; set; }

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
        
        private HashSet<string> _processedTradeIds = new HashSet<string>();
        private Dictionary<string, List<long>> _webhookTradeToPositions = new Dictionary<string, List<long>>();
        private DateTime _lastTradeIdCleanup = DateTime.UtcNow;
        private HttpListener _webhookListener;
        private StatusCard _statusCard;
        private bool _webhookServerError = false;
        private Dictionary<string, string> _symbolMappings = new Dictionary<string, string>
        {
            { "US30-Pepperstone", "US30" },
            { "NAS100-Pepperstone", "NAS100" },
            { "XAUUSD-Pepperstone", "XAUUSD" },
            { "XAGUSD-Pepperstone", "XAGUSD" },
            { "EURUSD-Pepperstone", "EURUSD" },
            { "GBPUSD-Pepperstone", "GBPUSD" },
            { "USDJPY-Pepperstone", "USDJPY" },
            { "AUDUSD-Pepperstone", "AUDUSD" },
            { "USDCAD-Pepperstone", "USDCAD" },
            { "NZDUSD-Pepperstone", "NZDUSD" },
            { "USDCHF-Pepperstone", "USDCHF" },
            { "EURGBP-Pepperstone", "EURGBP" },
            { "EURJPY-Pepperstone", "EURJPY" },
            { "GBPJPY-Pepperstone", "GBPJPY" },
            { "BTCUSD-Pepperstone", "BTCUSD" }
        };

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
            PrintLocal($"Bot Mode: {BotMode}");
            if (BotMode == BotModeType.Manual_Only)
                PrintLocal("  ⚠️ Webhooks disabled - Manual trading only");

            // Log risk mode configuration
            if (RiskMode == RiskModeType.Fixed)
            {
                if (FixedRiskUnit == FixedRiskUnit.Percent)
                    PrintLocal($"Risk Mode: Fixed {FixedRiskPercent:F2}% per trade");
                else
                    PrintLocal($"Risk Mode: Fixed ${FixedRiskDollar:F2} per trade");
            }
            else
            {
                PrintLocal($"Risk Mode: Dynamic | Starting Balance: ${StartingBalance:F2}, Current: ${Account.Balance:F2}");
                PrintLocal($"  Base Risk: {BaseRiskPercent:F2}%, Max Risk: {MaxRiskPercent:F2}%");
                PrintLocal($"  Growth Scaling: +{RiskIncreasePerStep:F0}% per {EquityGrowthStep:F1}% growth");
                PrintLocal($"  Drawdown Reduction: -{RiskReductionPerStep:F0}% per {DrawdownStep:F1}% DD");
                PrintLocal($"  Compounding: {(RiskCompounding ? "Enabled" : "Disabled")}");
                double currentRisk = GetCurrentRiskPercent();
                PrintLocal($"  Current Effective Risk: {currentRisk:F2}%");
            }
            
            PrintLocal($"Risk Gates: Max Drawdown={MaxDrawdown:F1}%, Max Daily Loss={MaxDailyLoss:F1}%");

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

            if (WebhookEnabled && BotMode == BotModeType.Auto)
            {
                try
                {
                    _webhookListener = new HttpListener();
                    _webhookListener.Prefixes.Add($"http://localhost:{WebhookPort}/webhook/");
                    _webhookListener.Start();
                    _webhookListener.BeginGetContext(WebhookListenerCallback, _webhookListener);
                    _webhookServerError = false;
                    PrintLocal($"[WEBHOOK-SERVER] Started successfully on port {WebhookPort}");
                }
                catch (Exception ex)
                {
                    _webhookServerError = true;
                    PrintLocal($"[WEBHOOK-SERVER] Failed to start: {ex.Message}");
                }
            }
            else if (BotMode == BotModeType.Manual_Only)
            {
                PrintLocal("[WEBHOOK-SERVER] Disabled - Bot in Manual Only mode");
            }
            else
            {
                PrintLocal("[WEBHOOK-SERVER] Disabled in settings");
            }

            if (StatusCardVisibility == StatusCardVisibility.Displayed)
            {
                _statusCard = new StatusCard(
                    this,
                    AccountName,
                    RiskMode,
                    WebhookPort,
                    BotMode,
                    _webhookServerError
                );

                var statusBorder = new Border
                {
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = "0 20 20 0"
                };
                statusBorder.Child = _statusCard;
                Chart.AddControl(statusBorder);
                
                _statusCard.RefreshPortStatus(_webhookServerError);
            }
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
            if (_webhookListener != null && _webhookListener.IsListening)
            {
                _webhookListener.Stop();
                _webhookListener.Close();
                PrintLocal("[WEBHOOK-SERVER] Stopped");
            }

            WriteLogsToFile();
            PrintLocal("Bot stopped.");
        }

        private void WebhookListenerCallback(IAsyncResult ar)
        {
            if (_webhookListener == null || !_webhookListener.IsListening)
                return;

            try
            {
                HttpListenerContext ctx = _webhookListener.EndGetContext(ar);
                _webhookListener.BeginGetContext(WebhookListenerCallback, _webhookListener);

                string body = ReadWebhookRequestBody(ctx);
                BeginInvokeOnMainThread(() => HandleWebhookRequest(body));

                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                _webhookServerError = true;
                if (_statusCard != null)
                {
                    BeginInvokeOnMainThread(() => _statusCard.RefreshPortStatus(_webhookServerError));
                }
                PrintLocal($"[WEBHOOK-SERVER] Listener callback error: {ex.Message}");
            }
        }

        private string ReadWebhookRequestBody(HttpListenerContext ctx)
        {
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                return reader.ReadToEnd();
            }
        }

        private void HandleWebhookRequest(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var webhook = ParseWebhookPayload(json);
                if (webhook == null)
                    return;

                string validationError;
                if (!ValidateWebhook(webhook, out validationError))
                {
                    PrintLocal($"[WEBHOOK] Rejected: {validationError}");
                    return;
                }

                ExecuteWebhookTrade(webhook);
            }
            catch (Exception ex)
            {
                PrintLocal($"[WEBHOOK] Error: {ex.Message}");
            }
        }

        private bool ValidateWebhook(WebhookPayload webhook, out string errorMessage)
        {
            errorMessage = null;

            if (BotMode == BotModeType.Manual_Only)
            {
                errorMessage = "Bot is in Manual Only mode - webhooks are disabled";
                return false;
            }

            if (!WebhookEnabled)
            {
                errorMessage = "Webhook server is disabled";
                return false;
            }

            if (string.IsNullOrEmpty(webhook.TradeId))
            {
                errorMessage = "Missing trade_id";
                return false;
            }

            if (IsTradeIdProcessed(webhook.TradeId))
            {
                errorMessage = $"Duplicate trade_id detected: {webhook.TradeId}";
                return false;
            }

            if (BrokerMatchValidation)
            {
                string webhookBroker = ExtractBrokerName(webhook.Instrument);
                if (!string.IsNullOrEmpty(webhookBroker) && 
                    !webhookBroker.Equals(ExpectedBrokerName, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Broker mismatch: Expected '{ExpectedBrokerName}', Got '{webhookBroker}'";
                    return false;
                }
            }

            if (!PassesSymbolFilter(webhook.Instrument))
            {
                errorMessage = $"Symbol filter mismatch: '{webhook.Instrument}' does not match filter '{SymbolFilter}'";
                return false;
            }

            // Only execute trades for ai_recommends_entry alert type
            if (string.IsNullOrEmpty(webhook.AlertType) || 
                !webhook.AlertType.Equals("ai_recommends_entry", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Alert type '{webhook.AlertType}' is not executable. Only 'ai_recommends_entry' triggers trades.";
                return false;
            }

            if (!CanTrade())
            {
                errorMessage = "Trading blocked by risk management gates";
                return false;
            }

            return true;
        }

        private string ExtractBrokerName(string instrument)
        {
            if (string.IsNullOrEmpty(instrument))
                return null;

            if (instrument.Contains("-"))
                return instrument.Split('-')[1];

            return null;
        }

        private bool PassesSymbolFilter(string webhookInstrument)
        {
            if (SymbolFilter == SymbolFilterOption.Accept_Any)
                return true;

            string filterSymbol = SymbolFilter.ToString().Replace("_", "-");
            string baseSymbol = ExtractBaseSymbol(webhookInstrument);
            string filterBaseSymbol = ExtractBaseSymbol(filterSymbol);

            return baseSymbol.Equals(filterBaseSymbol, StringComparison.OrdinalIgnoreCase);
        }

        private string ExtractBaseSymbol(string instrument)
        {
            if (string.IsNullOrEmpty(instrument))
                return string.Empty;

            // Extract base before broker suffix (e.g., US30.cash-FTMO -> US30.cash)
            string basePart = instrument.Contains("-") ? instrument.Split('-')[0] : instrument;
            
            // Remove .cash or .CASH suffix if present (e.g., US30.cash -> US30)
            if (basePart.Contains("."))
            {
                string[] parts = basePart.Split('.');
                if (parts.Length == 2 && parts[1].Equals("cash", StringComparison.OrdinalIgnoreCase))
                    return parts[0];
            }

            return basePart;
        }

        private string MapSymbolToMT(string webhookInstrument)
        {
            if (_symbolMappings.ContainsKey(webhookInstrument))
                return _symbolMappings[webhookInstrument];

            return ExtractBaseSymbol(webhookInstrument);
        }

        private double GetStopLossPrice(PriceZone stopLoss, string direction)
        {
            if (stopLoss == null)
                return 0;

            if (!stopLoss.IsZone)
                return stopLoss.Mid;

            if (SLZonePreference == SLZonePreference.Tight)
                return stopLoss.Tight;
            else
                return stopLoss.Wide;
        }

        private double GetTakeProfitPrice(PriceZone tp, string direction)
        {
            if (tp == null)
                return 0;

            if (!tp.IsZone)
                return tp.Mid;

            if (TPZonePreference == TPZonePreference.Early)
                return tp.Early;
            else
                return tp.Full;
        }

        private double CalculateSLDistanceInPips(double entryPrice, double slPrice, TradeType tradeType)
        {
            double priceDiff = Math.Abs(entryPrice - slPrice);
            return priceDiff / Symbol.PipSize;
        }

        private void LogWebhookEvent(string eventType, string message, WebhookPayload webhook = null)
        {
            string logMsg = $"[WEBHOOK-{eventType}] {message}";
            if (webhook != null)
                logMsg += $" | TradeID={webhook.TradeId}, Instrument={webhook.Instrument}";
            PrintLocal(logMsg);
        }

        private TradeType GetTradeType(string direction)
        {
            if (direction.Equals("LONG", StringComparison.OrdinalIgnoreCase))
                return TradeType.Buy;
            else if (direction.Equals("SHORT", StringComparison.OrdinalIgnoreCase))
                return TradeType.Sell;
            else
                throw new ArgumentException($"Invalid direction: {direction}");
        }

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

            if (totalVolume < Symbol.VolumeInUnitsMin)
                totalVolume = Symbol.VolumeInUnitsMin;

            if (hasTP3)
            {
                pos1Volume = totalVolume * (TP1Percent / 100.0);
                pos2Volume = totalVolume * (TP2Percent / 100.0);
                pos3Volume = totalVolume * (TP3Percent / 100.0);
            }
            else
            {
                double total12 = TP1Percent + TP2Percent;
                pos1Volume = totalVolume * (TP1Percent / total12);
                pos2Volume = totalVolume * (TP2Percent / total12);
                pos3Volume = 0;
            }

            pos1Volume = Symbol.NormalizeVolumeInUnits(pos1Volume, RoundingMode.ToNearest);
            pos2Volume = Symbol.NormalizeVolumeInUnits(pos2Volume, RoundingMode.ToNearest);
            if (hasTP3)
                pos3Volume = Symbol.NormalizeVolumeInUnits(pos3Volume, RoundingMode.ToNearest);

            if (pos1Volume < Symbol.VolumeInUnitsMin) pos1Volume = Symbol.VolumeInUnitsMin;
            if (pos2Volume < Symbol.VolumeInUnitsMin) pos2Volume = Symbol.VolumeInUnitsMin;
            if (hasTP3 && pos3Volume < Symbol.VolumeInUnitsMin) pos3Volume = Symbol.VolumeInUnitsMin;
        }

        public void ExecuteWebhookTrade(WebhookPayload webhook)
        {
            try
            {
                TradeType tradeType = GetTradeType(webhook.Direction);
                double currentPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;

                double slPrice = GetStopLossPrice(webhook.StopLoss, webhook.Direction);
                double tp1Price = GetTakeProfitPrice(webhook.TP1, webhook.Direction);
                double tp2Price = GetTakeProfitPrice(webhook.TP2, webhook.Direction);
                double tp3Price = GetTakeProfitPrice(webhook.TP3, webhook.Direction);

                if (slPrice <= 0 || tp1Price <= 0)
                    return;

                double slDistancePips = CalculateSLDistanceInPips(currentPrice, slPrice, tradeType);
                if (slDistancePips <= 0)
                    return;

                bool hasTP3 = (webhook.TP3 != null && tp3Price > 0);
                double tp1DistancePips = CalculateSLDistanceInPips(currentPrice, tp1Price, tradeType);
                double tp2DistancePips = tp2Price > 0 ? CalculateSLDistanceInPips(currentPrice, tp2Price, tradeType) : 0;
                double tp3DistancePips = hasTP3 ? CalculateSLDistanceInPips(currentPrice, tp3Price, tradeType) : 0;

                // Log acceptance with key details
                PrintLocal($"[WEBHOOK] Accepted: {webhook.Direction} {webhook.Instrument} | TradeID: {webhook.TradeId} | " +
                          $"SL: {slPrice:F5}, TP1: {tp1Price:F5}, TP2: {tp2Price:F5}" +
                          (hasTP3 ? $", TP3: {tp3Price:F5}" : "") +
                          $" | Confidence: {webhook.Confidence}%");

                double pos1, pos2, pos3;
                CalculateWebhookPositionSizes(slDistancePips, out pos1, out pos2, out pos3, hasTP3);

                var positionIds = new List<long>();
                double riskPercent = GetCurrentRiskPercent();
                double riskAmount = CalculateRiskAmount(slDistancePips);
                double totalVolume = pos1 + pos2 + pos3;

                // Create position labels with trade ID
                string shortTradeId = GetShortTradeId(webhook.TradeId);
                string label1 = $"{shortTradeId}_TP1";
                string label2 = $"{shortTradeId}_TP2";
                string label3 = $"{shortTradeId}_TP3";

                // Execute orders based on DefaultSLMode setting
                if (DefaultSLMode == SLCalculationMode.Price)
                {
                    // Use exact price levels from webhook
                    PrintLocal($"[WEBHOOK] Using exact price mode for SL/TP");
                    
                    var result1 = ExecuteMarketOrderWithExactPrices(tradeType, SymbolName, pos1, label1, slPrice, tp1Price);
                    if (result1.IsSuccessful)
                        positionIds.Add(result1.Position.Id);

                    var result2 = ExecuteMarketOrderWithExactPrices(tradeType, SymbolName, pos2, label2, slPrice, tp2Price);
                    if (result2.IsSuccessful)
                        positionIds.Add(result2.Position.Id);

                    if (hasTP3)
                    {
                        var result3 = ExecuteMarketOrderWithExactPrices(tradeType, SymbolName, pos3, label3, slPrice, tp3Price);
                        if (result3.IsSuccessful)
                            positionIds.Add(result3.Position.Id);
                    }
                }
                else
                {
                    // Use pip distance mode (legacy behavior)
                    PrintLocal($"[WEBHOOK] Using pip distance mode for SL/TP");
                    
#pragma warning disable 0618
                    var result1 = ExecuteMarketOrder(tradeType, SymbolName, pos1, label1, slDistancePips, tp1DistancePips);
#pragma warning restore 0618
                    if (result1.IsSuccessful)
                        positionIds.Add(result1.Position.Id);

#pragma warning disable 0618
                    var result2 = ExecuteMarketOrder(tradeType, SymbolName, pos2, label2, slDistancePips, tp2DistancePips);
#pragma warning restore 0618
                    if (result2.IsSuccessful)
                        positionIds.Add(result2.Position.Id);

                    if (hasTP3)
                    {
#pragma warning disable 0618
                        var result3 = ExecuteMarketOrder(tradeType, SymbolName, pos3, label3, slDistancePips, tp3DistancePips);
#pragma warning restore 0618
                        if (result3.IsSuccessful)
                            positionIds.Add(result3.Position.Id);
                    }
                }

                LinkWebhookTradeToPositions(webhook.TradeId, positionIds);
                MarkTradeIdProcessed(webhook.TradeId);

                _manualPositionOpen = true;
                _manualTradeCumulativeProfit = 0;

                // Broadcast webhook trade to receivers with TP/SL levels
                string action = (tradeType == TradeType.Buy) ? "Buy" : "Sell";
                SendJsonMessage(action, slDistancePips, slPrice, tp1Price, tp2Price, tp3Price, hasTP3, SymbolName, JsonSLFormat);

                // Log execution result
                PrintLocal($"[WEBHOOK] Executed: {positionIds.Count} position(s) opened (IDs: {string.Join(", ", positionIds)}) | " +
                          $"Risk: {riskPercent:F2}% (${riskAmount:F2}), Total: {totalVolume} units");
            }
            catch (Exception ex)
            {
                PrintLocal($"[WEBHOOK] Error: {ex.Message}");
            }
        }

        public bool CanTrade()
        {
            if (_stopTrading) return false;

            double eq = Account.Equity;
            
            // Max drawdown check
            double maxDrawdownThreshold = StartingBalance * (1.0 - (MaxDrawdown / 100.0));
            if (eq <= maxDrawdownThreshold)
            {
                PrintLocal($"Max Drawdown hit: Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2} (-{MaxDrawdown}%)");
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

        // -----------------------------------------------------------------------
        // Send HTTP messages to the configured ports:
        // action: "Buy"/"Sell" (with sl_pips or sl_price), or "Close All Positions"
        // -----------------------------------------------------------------------
        private void SendJsonMessage(string action, double? slPips, double? slPrice, 
            double? tp1Price, double? tp2Price, double? tp3Price, bool hasTP3, 
            string symbol, JSONSLFormat format)
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
                
                // Add TP levels with exact prices from webhook
                if (tp1Price.HasValue && tp2Price.HasValue)
                {
                    var tpLevels = new List<Dictionary<string, object>>();
                    
                    // TP1
                    double tp1Pips = CalculateSLDistanceInPips(currentPrice, tp1Price.Value, tradeType);
                    tpLevels.Add(new Dictionary<string, object>
                    {
                        ["label"] = "TP1Position",
                        ["tp_pips"] = tp1Pips,
                        ["tp_price"] = tp1Price.Value
                    });
                    
                    // TP2
                    double tp2Pips = CalculateSLDistanceInPips(currentPrice, tp2Price.Value, tradeType);
                    tpLevels.Add(new Dictionary<string, object>
                    {
                        ["label"] = "TP2Position",
                        ["tp_pips"] = tp2Pips,
                        ["tp_price"] = tp2Price.Value
                    });
                    
                    // TP3 (optional)
                    if (hasTP3 && tp3Price.HasValue && tp3Price.Value > 0)
                    {
                        double tp3Pips = CalculateSLDistanceInPips(currentPrice, tp3Price.Value, tradeType);
                        tpLevels.Add(new Dictionary<string, object>
                        {
                            ["label"] = "TP3Position",
                            ["tp_pips"] = tp3Pips,
                            ["tp_price"] = tp3Price.Value
                        });
                    }
                    
                    messageObj["tp_levels"] = tpLevels;
                    PrintLocal($"Broadcasted Trade: {action}, SL={slPrice:F5}, TP1={tp1Price:F5}, TP2={tp2Price:F5}" + 
                               (hasTP3 && tp3Price.HasValue ? $", TP3={tp3Price:F5}" : ""));
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

        private string GetShortTradeId(string tradeId)
        {
            if (string.IsNullOrEmpty(tradeId))
                return "MANUAL";
            
            // Remove dashes and take last 12 characters for uniqueness
            string cleaned = tradeId.Replace("-", "");
            
            if (cleaned.Length <= 12)
                return cleaned;
            
            return cleaned.Substring(cleaned.Length - 12);
        }

        private TradeResult ExecuteMarketOrderWithExactPrices(TradeType tradeType, string symbolName, double volume, 
            string label, double slPrice, double tpPrice)
        {
            // Execute market order without SL/TP first
            var result = ExecuteMarketOrder(tradeType, symbolName, volume, label, null, null);
            
            if (!result.IsSuccessful)
            {
                PrintLocal($"Failed to open position {label}: {result.Error}");
                return result;
            }
            
            // Immediately modify position with exact price levels
            var position = result.Position;
#pragma warning disable 0618
            var modifyResult = ModifyPosition(position, slPrice, tpPrice);
#pragma warning restore 0618
            
            if (!modifyResult.IsSuccessful)
            {
                PrintLocal($"Warning: Position {label} opened but failed to set exact SL/TP: {modifyResult.Error}");
                PrintLocal($"  Attempted SL: {slPrice:F5}, TP: {tpPrice:F5}");
            }
            else
            {
                PrintLocal($"Position {label} opened with exact prices - SL: {slPrice:F5}, TP: {tpPrice:F5}");
            }
            
            return result;
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
            double riskAmt = CalculateRiskAmount(finalDist);
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

            // Use MANUAL prefix for manual trades to distinguish from webhook trades
            ExecuteMarketOrderAsync(tradeType, SymbolName, pos1, "MANUAL_TP1", finalDist, tp1);
            ExecuteMarketOrderAsync(tradeType, SymbolName, pos2, "MANUAL_TP2", finalDist, tp2);
            ExecuteMarketOrderAsync(tradeType, SymbolName, pos3, "MANUAL_TP3", finalDist, tp3);

            // Calculate SL and TP prices for JSON message
            double currentPrice = (tradeType == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
            double slPrice = (tradeType == TradeType.Buy) 
                ? currentPrice - (finalDist * Symbol.PipSize) 
                : currentPrice + (finalDist * Symbol.PipSize);
            
            double tp1Price = CalculateTPPrice(tradeType, currentPrice, tp1);
            double tp2Price = CalculateTPPrice(tradeType, currentPrice, tp2);
            double tp3Price = CalculateTPPrice(tradeType, currentPrice, tp3);

            string act = (tradeType == TradeType.Buy) ? "Buy" : "Sell";
            SendJsonMessage(act, slPips, slPrice, tp1Price, tp2Price, tp3Price, true, SymbolName, JsonSLFormat);

            _manualPositionOpen = true;
            _manualTradeCumulativeProfit = 0;
        }

        public void CloseAllPositions()
        {
            SendJsonMessage("Close All Positions", null, null, null, null, null, false, SymbolName, JsonSLFormat);

            foreach (var pos in Positions)
                if (pos.SymbolName == SymbolName)
                    ClosePositionAsync(pos);
        }

        public void ToggleStatusCard()
        {
            if (_statusCard == null) return;

            if (_statusCard.IsVisible)
                _statusCard.Hide();
            else
                _statusCard.Show();
        }

        public void RefreshStatusCard()
        {
            if (_statusCard != null)
            {
                _statusCard.UpdateStatus(BotMode, _webhookServerError);
            }
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

                CleanupOldTradeIds();
            }
        }

        private void CheckHardStops()
        {
            if (_stopTrading) return;

            double eq = Account.Equity;
            
            // Max drawdown hard stop
            double maxDrawdownThreshold = StartingBalance * (1.0 - (MaxDrawdown / 100.0));
            if (eq <= maxDrawdownThreshold)
            {
                CloseAllPositions();
                _stopTrading = true;
                PrintLocal($"HARD STOP: Max Drawdown -{MaxDrawdown}% hit | Equity={eq:F2}, Threshold={maxDrawdownThreshold:F2}");
                return;
            }

            // Daily loss hard stop
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

        private WebhookPayload ParseWebhookPayload(string json)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                WebhookData data = null;

                // Try parsing as direct WebhookData first
                try
                {
                    data = JsonSerializer.Deserialize<WebhookData>(json, options);
                    if (data?.Instrument == null)
                        data = null;
                }
                catch
                {
                    data = null;
                }

                // If direct parsing failed, try wrapper structure
                if (data == null)
                {
                    try
                    {
                        var wrapper = JsonSerializer.Deserialize<WebhookPayloadWrapper>(json, options);
                        if (wrapper?.Data != null)
                            data = wrapper.Data;
                    }
                    catch
                    {
                        // Silent fail
                    }
                }

                if (data == null)
                    return null;

                var payload = new WebhookPayload
                {
                    AlertType = data.AlertType,
                    AlertId = data.AlertId,
                    TradeId = data.TradeId,
                    Instrument = data.Instrument,
                    Direction = data.Direction,
                    Time = data.Time,
                    EntryZone = data.EntryZone,
                    StopLoss = data.StopLoss,
                    TP1 = data.TP1,
                    TP2 = data.TP2,
                    TP3 = data.TP3,
                    AiDecision = data.AiDecision,
                    Confidence = data.Confidence
                };

                if (string.IsNullOrEmpty(payload.Instrument) || string.IsNullOrEmpty(payload.Direction) ||
                    payload.EntryZone == null || payload.StopLoss == null || payload.TP1 == null)
                    return null;

                return payload;
            }
            catch
            {
                return null;
            }
        }

        private bool IsTradeIdProcessed(string tradeId)
        {
            return _processedTradeIds.Contains(tradeId);
        }

        private void MarkTradeIdProcessed(string tradeId)
        {
            _processedTradeIds.Add(tradeId);
        }

        private void CleanupOldTradeIds()
        {
            _processedTradeIds.Clear();
            _webhookTradeToPositions.Clear();
        }

        private void LinkWebhookTradeToPositions(string tradeId, List<long> positionIds)
        {
            _webhookTradeToPositions[tradeId] = positionIds;
        }

        // ==================== NEW RISK CALCULATION SYSTEM ====================
        
        public double CalculateRiskAmount(double slDistancePips)
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
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

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
                Text = "Close\nAll",
                Margin = "10 0 10 0",
                MinHeight = 66,
                Style = closeStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = "10 10 10 10"
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
                Orientation = Orientation.Vertical,
                Margin = "0 5 0 0"
            };
            pipsModeButton = new Button
            {
                Text = "Pips",
                Width = 50,
                Margin = "0 0 0 3"
            };
            pipsModeButton.Click += (args) => SelectSLMode(SLCalculationMode.Pip_Dist);
            modeButtonStack.AddChild(pipsModeButton);

            priceModeButton = new Button
            {
                Text = "Price",
                Width = 50,
                Margin = "3 0 0 0"
            };
            priceModeButton.Click += (args) => SelectSLMode(SLCalculationMode.Price);
            modeButtonStack.AddChild(priceModeButton);

            modeStack.AddChild(modeButtonStack);
            
            main.AddChild(modeStack);

            var slPriceStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "0 0 10 0"
            };
            
            var slLabel = new TextBlock { Text = "SL (pips)" };
            slPriceStack.AddChild(slLabel);

            slTextBox = new TextBox
            {
                Text = defaultStopLossPips.ToString("F1"),
                Style = Styles.CreateInputStyle(),
                Width = 60,
                Margin = "0 5 0 0"
            };
            slTextBox.TextChanged += (args) => UpdateValues();
            slPriceStack.AddChild(slTextBox);

            var priceLabel = new TextBlock 
            { 
                Text = "SL Price",
                Margin = "0 8 0 0"
            };
            slPriceStack.AddChild(priceLabel);

            priceTextBox = new TextBox
            {
                Text = "0",
                Style = Styles.CreateInputStyle(),
                Width = 60,
                Margin = "0 5 0 0",
                IsEnabled = false
            };
            priceTextBox.TextChanged += (args) => UpdateValues();
            slPriceStack.AddChild(priceTextBox);
            
            main.AddChild(slPriceStack);

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
            
            var statusToggleButton = new Button
            {
                Text = "Status",
                Style = Styles.CreateLightGreyButtonStyle(),
                Margin = "0 5 0 0",
                Width = 60,
                Height = 25
            };
            statusToggleButton.Click += (args) => _bot.ToggleStatusCard();
            qtyRiskStack.AddChild(statusToggleButton);
            
            main.AddChild(qtyRiskStack);

            var buySellStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = "10 0 10 0",
                VerticalAlignment = VerticalAlignment.Center
            };

            buyButton = new Button
            {
                Text = "Buy",
                Width = 60,
                Height = 30,
                Margin = new Thickness(0, 0, 0, 6)
            };
            buyButton.Click += (args) => SelectDirection(TradeType.Buy);
            buySellStack.AddChild(buyButton);

            sellButton = new Button
            {
                Text = "Sell",
                Width = 60,
                Height = 30,
                Margin = new Thickness(0, 6, 0, 0)
            };
            sellButton.Click += (args) => SelectDirection(TradeType.Sell);
            buySellStack.AddChild(sellButton);

            main.AddChild(buySellStack);

            var placeStyle = Styles.CreatePlaceOrderButtonStyle(Color.FromHex("#058000"));
            placeOrderButton = new Button
            {
                Text = "Place\nOrder",
                Margin = "10 0 0 0",
                MinHeight = 66,
                Style = placeStyle,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = "10 10 10 10"
            };
            placeOrderButton.Click += (args) => ExecuteManual();
            main.AddChild(placeOrderButton);

            container.AddChild(main);

            var footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = "0 0 0 5"
            };

            var brandingText = new TextBlock
            {
                Text = "SkyAnalyst Automated Trader v1.0",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.6
            };
            footerPanel.AddChild(brandingText);
            container.AddChild(footerPanel);

            SelectDirection(TradeType.Buy);
            SelectSLMode(selectedSLMode);
            UpdateValues();
            return container;
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
                
                // Set default SL price to current price minus 0.2%
                double currentPrice = (selectedTradeType == TradeType.Buy) ? _symbol.Ask : _symbol.Bid;
                double defaultSLPrice = currentPrice * 0.998; // minus 0.2%
                priceTextBox.Text = defaultSLPrice.ToString("F5");
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

                var borderColor = (mainColor == Color.Red || mainColor == Color.FromHex("#058000")) ? Color.Red : Color.Transparent;
                style.Set(ControlProperty.BorderColor, borderColor, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderColor, borderColor, ControlState.LightTheme);
                style.Set(ControlProperty.BorderThickness, borderColor == Color.Red ? 2 : 0, ControlState.DarkTheme);
                style.Set(ControlProperty.BorderThickness, borderColor == Color.Red ? 2 : 0, ControlState.LightTheme);

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

            double riskAmt = _bot.CanTrade() ? _bot.CalculateRiskAmount(slPips) : 0.0;
            double fr = _bot.GetCurrentRiskPercent();

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
