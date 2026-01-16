# Webhook Integration Guide

This guide explains how to configure webhooks to receive AI entry signals from your monitored trades.

---

## Overview

When the AI approves an entry for one of your monitored trades, a webhook can be sent to your specified URL with all the trade details. This allows you to integrate with external systems like:

- Trading bots
- Custom notification services
- Trade journals
- Automation platforms (Zapier, Make, n8n, etc.)

---

## Setting Up Your Webhook

1. **Navigate to a monitored trade** in the Automations page
2. **Open the alert settings** by clicking the alert/settings button on the trade card
3. **Enable webhook delivery** in the "AI Entry Alert" section
4. **Enter your webhook URL** - must be a valid HTTPS URL

> ⚠️ **Security Note**: Your webhook URL should use HTTPS. Internal/private IP addresses are blocked for security.

---

## Webhook Payload

When the AI recommends entry, your webhook will receive a `POST` request with the following JSON payload:

```json
{
  "alert_type": "entry_approved",
  "alert_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "trade_id": "f0e1d2c3-b4a5-6789-0fed-cba987654321",
  "instrument": "XAUUSD-Pepperstone",
  "direction": "LONG",
  "time": "2025-12-17T22:30:00.000Z",
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
  "tp2": {
    "min": 2653.00,
    "max": 2657.00,
    "mid": 2655.00,
    "is_zone": true,
    "early": 2653.00,
    "full": 2657.00
  },
  "tp3": null,
  "ai_decision": "Entry trigger satisfied: M5 closed bullish at 2627.50 with strong momentum. RSI showing bullish divergence and price respected the entry zone support.",
  "confidence": 72
}
```

---

## Field Reference

### Top-Level Fields

| Field | Type | Description |
|-------|------|-------------|
| `alert_type` | string | Type of alert: `"entry_approved"`, `"entry_rejected"`, `"tp_hit"`, `"sl_hit"`, etc. |
| `alert_id` | string | Unique UUID of this alert record |
| `trade_id` | string | UUID of the monitored trade that triggered this alert |
| `instrument` | string | Full instrument name with broker suffix (e.g., `EURUSD-Pepperstone`) |
| `direction` | string | Trade direction: `"LONG"` or `"SHORT"` |
| `time` | string | ISO 8601 timestamp of when the signal was generated |
| `entry_zone` | object \| null | Entry zone with direction-aware helpers |
| `stop_loss` | object \| null | Stop loss level with direction-aware helpers |
| `tp1` | object \| null | Take Profit 1 with direction-aware helpers |
| `tp2` | object \| null | Take Profit 2 with direction-aware helpers |
| `tp3` | object \| null | Take Profit 3 with direction-aware helpers |
| `ai_decision` | string \| null | AI's reasoning for recommending entry |
| `confidence` | number \| null | AI confidence score (0-100) |

---

## Supported Instruments

The `instrument` field in the webhook payload uses internal symbol names. Here are all 15 supported instruments:

### Pepperstone Broker

| Internal Name | Display Name | Category |
|---------------|--------------|----------|
| `US30-Pepperstone` | US30 | Indices |
| `NAS100-Pepperstone` | NAS100 | Indices |
| `US500-Pepperstone` | US500 | Indices |
| `XAUUSD-Pepperstone` | XAUUSD | Commodities |
| `OIL-Pepperstone` | OIL | Commodities |
| `EURUSD-Pepperstone` | EURUSD | Forex |
| `GBPUSD-Pepperstone` | GBPUSD | Forex |
| `USDJPY-Pepperstone` | USDJPY | Forex |
| `USDCAD-Pepperstone` | USDCAD | Forex |
| `AUDUSD-Pepperstone` | AUDUSD | Forex |
| `DXY-Pepperstone` | DXY | Forex (Index) |
| `BTCUSD-Pepperstone` | BTCUSD | Crypto |

### HFM Broker

| Internal Name | Display Name | Category |
|---------------|--------------|----------|
| `US30-Hfm` | US30-Hfm | Indices |
| `NAS100-Hfm` | NAS100-Hfm | Indices |
| `US500-Hfm` | US500-Hfm | Indices |

> **Bot Matching**: Use the exact `instrument` string from the webhook to match trades in your bot. The format is always `{SYMBOL}-{BROKER}`.

---

## Price Field Structure

All price fields (`entry_zone`, `stop_loss`, `tp1`, `tp2`, `tp3`) use a **consistent object format** with direction-aware helper values:

```json
{
  "min": 2625.00,
  "max": 2628.00,
  "mid": 2626.50,
  "is_zone": true,
  "aggressive": 2628.00,
  "conservative": 2625.00
}
```

### Common Fields (All Price Objects)

| Field | Type | Description |
|-------|------|-------------|
| `min` | number | Lower bound of the zone (or exact price if single) |
| `max` | number | Upper bound of the zone (or exact price if single) |
| `mid` | number | Midpoint: `(min + max) / 2` |
| `is_zone` | boolean | `true` if min ≠ max, `false` if single price |

### Entry Zone Helper Fields

| Field | Description |
|-------|-------------|
| `aggressive` | Enter at edge of zone (LONG: max, SHORT: min) |
| `conservative` | Wait for better fill (LONG: min, SHORT: max) |

### Stop Loss Helper Fields

| Field | Description |
|-------|-------------|
| `tight` | Smaller risk, closer stop (LONG: max, SHORT: min) |
| `wide` | More room, further stop (LONG: min, SHORT: max) |

### Take Profit Helper Fields

| Field | Description |
|-------|-------------|
| `early` | Take profit sooner (LONG: min, SHORT: max) |
| `full` | Maximum profit target (LONG: max, SHORT: min) |

---

## Single Price vs Zone

When a level is a single price (not a zone), the object will have `min === max` and `is_zone: false`:

```json
{
  "min": 2640.00,
  "max": 2640.00,
  "mid": 2640.00,
  "is_zone": false,
  "early": 2640.00,
  "full": 2640.00
}
```

All helper fields will have the same value, so your bot can use any of them.

---

## Example: Handling in Code

### JavaScript/Node.js

```javascript
app.post('/webhook', (req, res) => {
  const { instrument, direction, entry_zone, stop_loss, tp1, tp2, tp3, ai_decision, confidence } = req.body;
  
  if (!entry_zone) {
    return res.status(400).send('Missing entry zone');
  }
  
  // Use direction-aware helpers - no need to check direction yourself!
  const entryPrice = entry_zone.aggressive;  // Or conservative, or mid
  const slPrice = stop_loss?.tight;          // Or wide
  const tp1Price = tp1?.early;               // Or full
  
  // Or use mid for balanced approach
  const balancedEntry = entry_zone.mid;
  const balancedSL = stop_loss?.mid;
  
  // Check if it's a zone or single price
  if (entry_zone.is_zone) {
    console.log(`Entry zone: ${entry_zone.min} - ${entry_zone.max}`);
  } else {
    console.log(`Entry price: ${entry_zone.min}`);
  }
  
  console.log(`AI Entry Signal: ${direction} ${instrument} @ ${entryPrice}`);
  console.log(`SL: ${slPrice}, TP1: ${tp1Price}, TP2: ${tp2?.early}, TP3: ${tp3?.early}`);
  console.log(`Confidence: ${confidence}% - ${ai_decision}`);
  
  res.status(200).send('OK');
});
```

### Python

```python
from flask import Flask, request

app = Flask(__name__)

@app.route('/webhook', methods=['POST'])
def handle_webhook():
    data = request.json
    
    entry_zone = data.get('entry_zone')
    stop_loss = data.get('stop_loss')
    
    if not entry_zone:
        return 'Missing entry zone', 400
    
    # Use direction-aware helpers directly
    entry_price = entry_zone['aggressive']  # Or 'conservative' or 'mid'
    sl_price = stop_loss['tight'] if stop_loss else None  # Or 'wide'
    
    # Get TP prices
    tp1 = data.get('tp1')
    tp1_price = tp1['early'] if tp1 else None  # Or 'full'
    
    # Check if zone
    if entry_zone['is_zone']:
        print(f"Entry zone: {entry_zone['min']} - {entry_zone['max']}")
    else:
        print(f"Entry price: {entry_zone['min']}")
    
    print(f"AI Entry: {data['direction']} {data['instrument']} @ {entry_price}")
    print(f"SL: {sl_price}, TP1: {tp1_price}")
    
    return 'OK', 200
```

### MQL5 (MetaTrader)

```mql5
// Parse JSON and use helper fields
double entryPrice = JsonGetDouble(json, "entry_zone.aggressive");
double stopLoss = JsonGetDouble(json, "stop_loss.tight");
double takeProfit = JsonGetDouble(json, "tp1.early");

// Open trade with parsed values
trade.Buy(lotSize, symbol, entryPrice, stopLoss, takeProfit);
```

---

## Response Requirements

Your webhook endpoint should:

- Return a **2xx status code** (200, 201, 204) to acknowledge receipt
- Respond within **5 seconds** (requests timeout after 5s)
- Accept `Content-Type: application/json`

---

## Retry Policy

Currently, webhooks are **not retried** on failure. If your endpoint is unavailable or returns an error, the webhook delivery will be marked as failed. The alert will still be created in-app regardless of webhook delivery status.

---

## Troubleshooting

### Webhook not receiving data?

1. **Check URL** - Ensure it's a valid HTTPS URL
2. **Check firewall** - Your endpoint must be publicly accessible
3. **Check logs** - View edge function logs in Supabase dashboard
4. **Verify enabled** - Both "Webhook" toggle AND "AI Entry Alert" must be enabled

### Receiving null fields?

- `tp2` and `tp3` will be `null` if not all take profit levels were defined
- `ai_decision` may be `null` if the AI didn't provide reasoning
- `confidence` may be `null` in edge cases
- `stop_loss` may be `null` if not defined in the original analysis

---

## Questions?

If you have questions about webhook integration, reach out through the app's support channels.