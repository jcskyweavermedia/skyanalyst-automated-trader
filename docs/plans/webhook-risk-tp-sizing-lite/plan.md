# Implementation Plan — webhook-risk-tp-sizing-lite

**Target file (single):** `D:\iCloud Drive\iCloudDrive\Skyweaver Trading\Bot Codes\Trading Panel New Reversal\SkyAnalyst Automated Trader Lite.cs`
**Out of scope:** Pro variant (`SkyAnalyst Automated Trader.cs`), webhook port hardening propagation, automated tests.
**Validation:** User will smoke-test in cTrader after implementation.

---

## Goal

Three changes to the Lite bot:

1. **Webhook can dictate TP1/TP2/TP3 sizing percentages** (currently bot-only).
2. **Webhook can dictate the per-trade risk** (percent or dollar; currently bot-only).
3. **Fix SL/TP offset bug** — currently uses directional signs producing widen/narrow; replace with uniform price shift so a negative offset shifts every level down and a positive offset shifts every level up, regardless of Buy/Sell.

Two new visible bot parameters control the source of (1) and (2): `Risk Source` and `TP Source`, each `App | Bot`, both default `Bot` for backwards compatibility.

---

## Locked decisions

1. Two visible bot params, both default `Bot`: `Risk Source` (App/Bot), `TP Source` (App/Bot). Single shared enum `RiskTPSource { App, Bot }`.
2. Resolution per category:
   - `Bot` → use bot params, ignore webhook risk/TP fields entirely.
   - `App` → per-field fallback: use webhook value if non-null, else fall back to bot param.
3. New optional `WebhookData` fields: `risk` (`double?`), `risk_unit` (`string`, "percent"|"dollar", required iff `risk` present), `tp1_percent`/`tp2_percent`/`tp3_percent` (`double?` each).
4. **Validation rule for webhook risk**: if `risk` is present and (`risk <= 0` OR `risk_unit` missing/not-in-["percent","dollar"] case-insensitive) → reject the webhook in `ValidateWebhook` regardless of `RiskSource` setting (structural validation).
5. No TP percent validation. Trust webhook. Existing `activePercentSum` redistribution handles 100/0/0.
6. SL/TP offset: replace directional signs with uniform shift `slPrice/tp1/tp2/tp3 += offsetPrice` regardless of trade direction.
7. **StatusCard**: not updated in this plan. The two new params are visible in cTrader's parameter UI — sufficient for now. Revisit if user wants on-chart indicator.
8. **`SendJsonMessage` broadcast**: not extended with TP percents in this plan. Out of scope; receivers don't currently consume them. Revisit if needed.
9. **App-risk path in Lite**: Lite hardcodes `RiskMode = Fixed`, so the App branch is straightforward — `percent` → `Account.Balance * risk / 100`; `dollar` → flat dollar value. No interaction with Dynamic scaling logic.

---

## Architecture summary

### Data flow when a webhook arrives

```
HandleWebhookRequest (~L1113)
  └─> ParseWebhookPayload (~L2095)         [T2/T3: deserialize new fields, copy in WebhookPayload initializer]
  └─> ValidateWebhook (~L1139)             [T4: structural reject of malformed risk fields]
  └─> ExecuteWebhookTrade (~L1357)
        ├─ Resolve effectiveRiskAmount     [T8: based on RiskSource]
        ├─ Resolve effective TP percents   [T9: based on TpSource, per-field fallback]
        ├─ Re-derive hasTP2 / hasTP3 from EFFECTIVE percents  [T9 — critical, was using bot fields]
        ├─ Apply uniform SL/TP offset      [T10: bug fix, no directional signs]
        └─ CalculateWebhookPositionSizes(slDist, riskAmount, tp1Pct, tp2Pct, tp3Pct, hasTP2, hasTP3, out ...)
                                           [T6: refactored signature — accepts riskAmount + TP percents as params,
                                            no longer reads bot fields, no longer calls CalculateRiskAmount internally]
```

### Why the refactor in T6 is structural-blocking for T8

Today `CalculateWebhookPositionSizes` calls `CalculateRiskAmount(slDistancePips)` *inside* itself (~L1326) and reads bot's `TP1Percent`/`TP2Percent`/`TP3Percent` fields directly. If T8 only changes "what risk we'd want to use" but the helper recomputes from bot params anyway, T8 is a no-op. T6 must lift both `riskAmount` AND the three TP percents up to parameters so T8 + T9 can inject effective values.

### Why `hasTP2` / `hasTP3` derivation is critical

Currently at L1392-1393:
```csharp
bool hasTP2 = TP2Percent > 0 && tp2Price > 0;
bool hasTP3 = TP3Percent > 0 && webhook.TP3 != null && tp3Price > 0;
```
Reads bot fields. Under App TP source with `bot.TP2Percent=0` but `webhook.tp2_percent=30`, `hasTP2` would silently stay `false` → TP2 position never opens. T9 must rewrite these to use the **effective** TP percents, not bot fields.

---

## Milestones

- **M1 — Foundation (additive, no behavior change):** T1, T2, T3, T5, T7
- **M2 — Refactor + bug fix (still no new behavior):** T6, T10
- **M3 — Validation:** T4
- **M4 — Wire the feature + logging:** T8, T9, T11

After each milestone the file should compile in cAlgo. After M4 the user can smoke-test by flipping the new params.

---

## Tasks

### [x] T1 — Add `RiskTPSource` enum

**Purpose:** Shared enum for the two new bot params.
**File / location:** Lite.cs, in `cAlgo.Robots` namespace, adjacent to existing enums (~L41-117).
**Dependencies:** none.
**Acceptance criteria:**
- `public enum RiskTPSource { App, Bot }` declared inside `namespace cAlgo.Robots`.
- No `using` aliases needed.
- File compiles unchanged otherwise.
**Security:** N/A (enum declaration, no input handling).

---

### [x] T2 — Extend `WebhookData` with optional risk and TP-percent fields

**Purpose:** Schema additions for incoming webhook.
**File / location:** Lite.cs, `WebhookData` class at ~L152-192.
**Dependencies:** none.
**Acceptance criteria:**
- Five new properties added with exact JSON keys via `[JsonPropertyName(...)]`:
  - `public double? Risk { get; set; }` — `"risk"`
  - `public string RiskUnit { get; set; }` — `"risk_unit"`
  - `public double? Tp1Percent { get; set; }` — `"tp1_percent"`
  - `public double? Tp2Percent { get; set; }` — `"tp2_percent"`
  - `public double? Tp3Percent { get; set; }` — `"tp3_percent"`
- `WebhookPayload : WebhookData` continues to inherit all fields without modification.
- No defaults set on these properties (must remain `null`/default when JSON omits them).
- File compiles.
**Security:** Inputs are deserialized from network — validated downstream in T4. No raw use yet.

---

### [x] T3 — Map new fields in `ParseWebhookPayload`'s explicit object initializer

**Purpose:** `ParseWebhookPayload` at ~L2095 builds a `WebhookPayload` via `new WebhookPayload { ... }` (L2137-2152) and explicitly copies each `WebhookData` field. Without this task, the new fields will silently be `null` on the payload that flows to `ExecuteWebhookTrade` even though they parsed correctly.
**File / location:** Lite.cs, `ParseWebhookPayload` at L2137-2152 (the `var payload = new WebhookPayload { ... };` block specifically).
**Dependencies:** T2.
**Acceptance criteria:**
- Five new lines added inside the `new WebhookPayload { ... }` initializer:
  - `Risk = data.Risk,`
  - `RiskUnit = data.RiskUnit,`
  - `Tp1Percent = data.Tp1Percent,`
  - `Tp2Percent = data.Tp2Percent,`
  - `Tp3Percent = data.Tp3Percent,`
- When incoming JSON omits these fields, the corresponding payload properties remain `null`. (Sanity smoke-test: send a current legacy payload, confirm `payload.Risk == null` etc.)
- No exception thrown when fields absent.
- File compiles.
**Security:** Pure assignment from already-deserialized object; no new attack surface.

---

### [x] T4 — Validate webhook risk fields in `ValidateWebhook`

**Purpose:** Reject structurally-malformed risk payloads. Always-on (not gated on `RiskSource`) per locked decision #4 — webhook is malformed regardless of how the bot intends to use it.
**File / location:** Lite.cs, `ValidateWebhook` at ~L1139-1195. Insert new check before the `return true` line.
**Dependencies:** T2.
**Acceptance criteria:**
- If `webhook.Risk.HasValue`:
  - Reject if `webhook.Risk.Value <= 0` with message `"Webhook risk must be > 0"`.
  - Reject if `string.IsNullOrWhiteSpace(webhook.RiskUnit)` with message `"Webhook risk specified without risk_unit"`.
  - Reject if `webhook.RiskUnit` is not equal (case-insensitive, `OrdinalIgnoreCase`) to `"percent"` or `"dollar"` with message `"Webhook risk_unit must be 'percent' or 'dollar' (got '<value>')"`.
- If `webhook.Risk` is null, this validation is skipped entirely (no rejection from missing/invalid risk_unit alone).
- Rejection uses the existing `errorMessage` out-parameter pattern; no other validation rules altered.
- No TP percent validation added (per locked decision #5).
- File compiles.
**Security:** This IS the input-validation boundary for the new webhook fields. Must use `OrdinalIgnoreCase` for `risk_unit` comparison. Must not call any broker API or mutate state. Error messages must include enough detail for debugging but not echo the entire payload.

---

### [x] T5 — Add `RiskSource` and `TpSource` bot parameters

**Purpose:** Expose the two visible toggles in cTrader UI.
**File / location:** Lite.cs, parameter block at ~L460-568 — insert after the existing "Trading Bridge Settings" group (after `SLTPOffsetPips` at L472).
**Dependencies:** T1.
**Acceptance criteria:**
- Two new properties:
  ```csharp
  [Parameter("Risk Source", Group = "Trading Bridge Settings", DefaultValue = RiskTPSource.Bot)]
  public RiskTPSource RiskSource { get; set; }

  [Parameter("TP Source", Group = "Trading Bridge Settings", DefaultValue = RiskTPSource.Bot)]
  public RiskTPSource TpSource { get; set; }
  ```
- Both default to `Bot` (preserves current behavior).
- Both appear in the same Group as the existing webhook-related params (Symbol, SL/TP Offset).
- File compiles.
**Security:** N/A (declarative parameter; user-supplied via cTrader UI, not network).

---

### [x] T6 — Refactor `CalculateWebhookPositionSizes` to accept `riskAmount` AND TP percents as parameters

**Purpose:** Decouple risk and TP-percent inputs from internal field reads so T8 and T9 can inject effective values. **Pure refactor — no behavior change at this stage.** The single caller (in `ExecuteWebhookTrade`) is updated to pass the bot-derived values it would compute today.
**File / location:**
- `CalculateWebhookPositionSizes` at ~L1318-1355 (signature + body).
- Single caller in `ExecuteWebhookTrade` at ~L1406.
**Dependencies:** none (pure refactor, but should be done before T8/T9 which rely on the new signature).
**Acceptance criteria:**
- New signature:
  ```csharp
  private void CalculateWebhookPositionSizes(
      double slDistancePips,
      double riskAmount,
      double tp1Percent,
      double tp2Percent,
      double tp3Percent,
      out double pos1Volume,
      out double pos2Volume,
      out double pos3Volume,
      bool hasTP2,
      bool hasTP3)
  ```
- Internal `CalculateRiskAmount(slDistancePips)` call at L1326 is **removed** — the helper now uses the `riskAmount` parameter directly.
- Internal references to bot fields `TP1Percent`/`TP2Percent`/`TP3Percent` (at L1335-1344) are **replaced** with the new parameters `tp1Percent`/`tp2Percent`/`tp3Percent`.
- The single caller in `ExecuteWebhookTrade` (~L1406) is updated to:
  - Compute `double riskAmount = CalculateRiskAmount(slDistancePips);` immediately before the call (preserves current behavior).
  - Pass bot's `TP1Percent`/`TP2Percent`/`TP3Percent` as the three TP-percent params.
- After this task, behavior is identical to before. Verify by re-reading the call path.
- No other callers of this method exist in the file (verify via grep before editing).
- File compiles.
**Security:** N/A (pure internal refactor).

---

### [x] T7 — Add `CalculateWebhookRiskAmount(WebhookData webhook)` helper

**Purpose:** Single place for the App-risk computation.
**File / location:** Lite.cs, near `CalculateRiskAmount` / `CalculateFixedRiskAmount` at ~L2205-2227.
**Dependencies:** T2.
**Acceptance criteria:**
- Signature: `private double CalculateWebhookRiskAmount(WebhookData webhook)` (takes the base `WebhookData` so it works for both `WebhookPayload` and any caller).
- Behavior:
  - If `webhook.RiskUnit.Equals("percent", StringComparison.OrdinalIgnoreCase)` → return `Account.Balance * webhook.Risk.Value / 100.0`.
  - Else (assumed `"dollar"` after T4 validation) → return `webhook.Risk.Value`.
- Assumes inputs are pre-validated by T4 — no defensive null/empty checks inside (keeps single source of truth for validation).
- Pure function: no state mutation, no broker API calls, no logging.
- File compiles.
**Security:** Reads validated webhook fields; uses `OrdinalIgnoreCase` for unit comparison (matches T4).

---

### [x] T8 — Wire `RiskSource` resolution in `ExecuteWebhookTrade`

**Purpose:** Apply locked decision #2 for risk.
**File / location:** Lite.cs, `ExecuteWebhookTrade` at ~L1357-1493 — at the call site for `CalculateWebhookPositionSizes` (~L1406, post-T6).
**Dependencies:** T2, T3, T5, T6, T7.
**Acceptance criteria:**
- Compute `effectiveRiskAmount` immediately before calling `CalculateWebhookPositionSizes`:
  ```csharp
  double effectiveRiskAmount =
      (RiskSource == RiskTPSource.App && webhook.Risk.HasValue)
          ? CalculateWebhookRiskAmount(webhook)
          : CalculateRiskAmount(slDistancePips);
  ```
- Pass `effectiveRiskAmount` to `CalculateWebhookPositionSizes` (replaces the `riskAmount` computed in T6's caller-site fix).
- When `RiskSource == Bot`, `webhook.Risk` is **ignored** even if non-null (the ternary's first branch is gated on `RiskSource == App`).
- When `RiskSource == App` and `webhook.Risk` is null, falls back to `CalculateRiskAmount(slDistancePips)`.
- The `riskAmount` variable used downstream for logging at L1410 is updated to `effectiveRiskAmount` so logs reflect what was actually used (resolves part of devil's-advocate concern; rest in T11).
- No other position-sizing math altered.
- File compiles.
**Security:** Uses pre-validated webhook fields. Risk amount is bounded by validation (`> 0`) and webhook unit (`percent` capped by Account.Balance, `dollar` raw). No new attack surface beyond T4.

---

### [x] T9 — Wire `TpSource` resolution in `ExecuteWebhookTrade` AND fix `hasTP2`/`hasTP3` derivation

**Purpose:** Apply locked decision #2 for TP percents AND fix the silent-failure where bot's `TP2Percent=0` would suppress an App-supplied TP2.
**File / location:** Lite.cs, `ExecuteWebhookTrade` at ~L1357-1493 — specifically L1392-1393 (`hasTP2`/`hasTP3` derivation) and the call site for `CalculateWebhookPositionSizes` (~L1406, post-T6/T8).
**Dependencies:** T2, T3, T5, T6.
**Acceptance criteria:**
- Compute three `effective` TP percents BEFORE the `hasTP2`/`hasTP3` derivation:
  ```csharp
  double effectiveTp1Percent = (TpSource == RiskTPSource.App && webhook.Tp1Percent.HasValue)
      ? webhook.Tp1Percent.Value : TP1Percent;
  double effectiveTp2Percent = (TpSource == RiskTPSource.App && webhook.Tp2Percent.HasValue)
      ? webhook.Tp2Percent.Value : TP2Percent;
  double effectiveTp3Percent = (TpSource == RiskTPSource.App && webhook.Tp3Percent.HasValue)
      ? webhook.Tp3Percent.Value : TP3Percent;
  ```
- Replace L1392-1393 to use effective values:
  ```csharp
  bool hasTP2 = effectiveTp2Percent > 0 && tp2Price > 0;
  bool hasTP3 = effectiveTp3Percent > 0 && webhook.TP3 != null && tp3Price > 0;
  ```
- Pass `effectiveTp1Percent`/`effectiveTp2Percent`/`effectiveTp3Percent` to `CalculateWebhookPositionSizes` (replaces bot fields T6's caller passed).
- Per-field fallback semantics: each TP slot independently falls back to its bot value if webhook didn't specify it. (Matches devil's-advocate validation that mixed-source is acceptable.)
- When `TpSource == Bot`, all three effective values equal bot fields (current behavior preserved).
- No TP percent validation added (per locked decision #5). Edge-case note: if App sends `tp1_percent=0, tp2_percent=0, tp3_percent=0` (which user said won't happen), `activePercentSum` falls back to 100 and a min-volume TP1 position opens. Documented; not blocked.
- File compiles.
**Security:** Effective values are bounded by trust (no validation). Documented edge case acknowledged. No raw user input beyond webhook fields already trusted by locked decision #5.

---

### [x] T10 — Fix SL/TP offset to uniform price shift

**Purpose:** Bug fix per locked decision #6.
**File / location:** Lite.cs, `ExecuteWebhookTrade` at L1369-1383.
**Dependencies:** none.
**Acceptance criteria:**
- The block becomes:
  ```csharp
  if (SLTPOffsetPips != 0)
  {
      double offsetPrice = SLTPOffsetPips * Symbol.PipSize;
      slPrice += offsetPrice;
      tp1Price += offsetPrice;
      if (tp2Price > 0) tp2Price += offsetPrice;
      if (tp3Price > 0) tp3Price += offsetPrice;
      PrintLocal($"[WEBHOOK] Applied {SLTPOffsetPips:+0.0;-0.0} pip uniform shift to SL/TP prices");
  }
  ```
- The `slSign` and `tpSign` local variables are **removed**.
- The misleading `// Positive offset = widen ...` comment at L1373 is **removed** (it described the buggy semantics).
- The log message updated from `"pip offset to SL/TP prices"` → `"pip uniform shift to SL/TP prices"` so it's clear in retrospective logs which version produced the trade.
- No directional branching anywhere in this block.
- Behavior verified by mental walkthrough:
  - Buy with `+20`: slPrice +20, tp1+20, tp2+20, tp3+20 (all shift up).
  - Buy with `-20`: all shift down by 20.
  - Sell with `+20`: all shift up by 20.
  - Sell with `-20`: all shift down by 20.
- File compiles.
**Security:** N/A (price arithmetic).

---

### [x] T11 — Update execution-result logging to reflect effective sources and amounts

**Purpose:** The log at L1487 (`Risk: {riskPercent:F2}% (${riskAmount:F2}), Total: {totalVolume} units`) currently uses `GetCurrentRiskPercent()` and `CalculateRiskAmount()` — both bot-driven. Under App-risk this would silently print the bot's risk, sabotaging smoke-test verification.
**File / location:** Lite.cs, `ExecuteWebhookTrade` execution-success log around L1486-1487.
**Dependencies:** T8, T9.
**Acceptance criteria:**
- Replace the existing log with one that reports the actually-used values and source labels:
  ```csharp
  string riskSourceLabel = (RiskSource == RiskTPSource.App && webhook.Risk.HasValue) ? "App" : "Bot";
  string tpSourceLabel = (TpSource == RiskTPSource.App &&
      (webhook.Tp1Percent.HasValue || webhook.Tp2Percent.HasValue || webhook.Tp3Percent.HasValue)) ? "App" : "Bot";
  PrintLocal($"[WEBHOOK] Executed: {positionIds.Count} position(s) opened (IDs: {string.Join(", ", positionIds)}) | " +
             $"Risk[{riskSourceLabel}]: ${effectiveRiskAmount:F2}, " +
             $"TP[{tpSourceLabel}]: {effectiveTp1Percent:F0}/{effectiveTp2Percent:F0}/{effectiveTp3Percent:F0}, " +
             $"Total: {totalVolume} units");
  ```
- The existing `riskPercent` variable computed via `GetCurrentRiskPercent()` may be removed if no longer referenced; keep it if other lines still need it.
- File compiles.
**Security:** N/A (logging). Avoid logging the raw webhook payload.

---

## Dependency graph

```
T1 ──────────► T5 ──────────► T8 ─┐
                                  │
T2 ──┬─► T3 ─────────────────► T8 │
     │                            ├──► T11
     ├─► T4                       │
     │                            │
     └─► T7 ──────────────────► T8 ─┘
                                  │
T2 ──┬─► T3 ─────────────────► T9 ─┐
     │                              ├──► T11
T1 ──┴─► T5 ─────────────────► T9 ─┤
                                    │
T6 (independent refactor) ────► T8, T9 ─┘

T10 (independent bug fix)
```

## Recommended execution order (serial — single file, conflicting edits)

Devil's-advocate flagged that T6, T9, T10, T11 all edit `ExecuteWebhookTrade` and cannot run in parallel. Same for T1+T5 sharing the parameter block region. The plan is small enough that serial execution is simpler and safer than parallel waves.

**Order:**
1. **T1** — Add enum.
2. **T2** — Add WebhookData fields.
3. **T3** — Map fields in ParseWebhookPayload.
4. **T5** — Add bot params (after T1).
5. **T7** — Add CalculateWebhookRiskAmount helper.
6. **T6** — Refactor CalculateWebhookPositionSizes (lift `riskAmount` and TP percents to params; caller passes bot-derived values).
7. **T10** — Fix SL/TP offset (independent, but easier to do before T8/T9 which sit nearby in `ExecuteWebhookTrade`).
8. **T4** — Add webhook risk validation.
9. **T8** — Wire RiskSource resolution in `ExecuteWebhookTrade`.
10. **T9** — Wire TpSource resolution + fix `hasTP2`/`hasTP3` derivation.
11. **T11** — Update execution-success logging.

**Compile-and-stop checkpoint after T6 and again after T11** — these are the riskiest moments for breakage.

---

## Acceptance criteria (feature-level)

After all 11 tasks:

- [ ] File compiles in cAlgo IDE without warnings introduced by this work.
- [ ] **Smoke test 1 (defaults, current behavior preserved):** With both `Risk Source = Bot` and `TP Source = Bot`, send a webhook identical to today's payload. Bot opens a trade with risk and TP allocations from bot params, exactly as before. Log line shows `Risk[Bot]: ...` and `TP[Bot]: ...`.
- [ ] **Smoke test 2 (App risk, percent):** Set `Risk Source = App`. Send webhook with `"risk": 2, "risk_unit": "percent"`. Bot opens trade risking 2% of account balance regardless of bot's `Fixed Risk (%)`. Log shows `Risk[App]: $<2% of balance>`.
- [ ] **Smoke test 3 (App risk, dollar):** Set `Risk Source = App`. Send webhook with `"risk": 100, "risk_unit": "dollar"`. Bot risks $100 flat. Log shows `Risk[App]: $100.00`.
- [ ] **Smoke test 4 (App TP, 100/0/0):** Set `TP Source = App`. Send webhook with `"tp1_percent": 100, "tp2_percent": 0, "tp3_percent": 0`. Bot opens ONE position (TP1 only, full size). Log shows `TP[App]: 100/0/0`.
- [ ] **Smoke test 5 (App TP activates dormant slot):** Bot's `TP2Percent` set to 0 in params. Set `TP Source = App`. Send webhook with `"tp2_percent": 30`. Bot opens TP2 position (proves the `hasTP2` fix). Log shows `TP[App]: 60/30/10` (or whatever bot/effective mix).
- [ ] **Smoke test 6 (validation reject):** Send webhook with `"risk": 2` but no `risk_unit`. Bot rejects with the validation message. No trade opened.
- [ ] **Smoke test 7 (validation reject):** Send webhook with `"risk": 2, "risk_unit": "%"`. Bot rejects (only "percent"/"dollar" accepted). No trade opened.
- [ ] **Smoke test 8 (offset uniform shift, negative):** Set `SLTPOffsetPips = -20`. Send a Buy webhook. Verify in cTrader that SL is exactly 20 pips below the signal SL, all TPs are 20 pips below the signal TPs. (Previously SL would have moved UP and TP DOWN — wrong direction.)
- [ ] **Smoke test 9 (offset uniform shift, positive Sell):** Set `SLTPOffsetPips = +20`. Send a Sell webhook. Verify SL and all TPs shifted UP by 20 pips uniformly.

---

## Devil's-advocate challenges and resolutions

| # | Challenge | Resolution |
|---|---|---|
| B1 | T6 left `CalculateRiskAmount` call inside the helper — App risk would be silently overridden | T6 AC now requires removing the internal call AND lifting `riskAmount` to a parameter |
| B2 | T8/T9 missing transitive deps on T2 and T3 (ParseWebhookPayload's explicit copy at L2137 silently nulls fields) | T8 deps = T2, T3, T5, T6, T7. T9 deps = T2, T3, T5, T6. T3 made explicit about touching the L2137 initializer block |
| B3 | `hasTP2`/`hasTP3` derivation at L1392-1393 reads bot fields — App TP percents in dormant slots silently dropped | T9 AC now explicitly rewrites L1392-1393 to use effective values |
| B4 | Wave A parallelism unsafe — T6, T9, T10 all edit `ExecuteWebhookTrade` | Plan switched to serial execution. Documented in execution order section |
| S1 | Log at L1487 uses bot-driven values — would mask App-risk in smoke tests | T11 added |
| S2 | T4 always-on rejects malformed risk even when `Bot` mode would ignore | Resolved as **always-on** (locked decision #4). Documented rationale in plan |
| S3 | T10 left misleading "widen" comment | T10 AC now requires removing the comment and updating log text |
| S4 | StatusCard not updated — does user need on-chart indicator? | Decision: not in this plan. cTrader UI shows new params. Revisit if requested |
| S5 | `SendJsonMessage` broadcast doesn't include TP percents | Decision: not in this plan. Receivers don't consume; revisit if needed |
| M1 | Naming consistency — `TpSource` vs `TPSource` | Plan uses `TpSource` (matches `Tp1Percent` etc. in WebhookData; aligns with .NET-style PascalCase for two-letter+ acronyms) |
| M2 | Verify `ExecuteManualTrade` is genuinely uncoupled | Documented in plan; implementer verifies via grep that only `CalculateWebhookPositionSizes` consumes the new wiring |

---

## Out of scope (explicit)

- Pro variant (`SkyAnalyst Automated Trader.cs`) — separate plan after Lite is validated.
- Webhook port hardening propagation to Pro — separate plan.
- Updating `SendJsonMessage` broadcast to include TP percents.
- Updating `StatusCard` to display the new sources.
- Automated tests (cAlgo cBots not easily unit-testable; manual smoke test only).
- Changes to `ExecuteManualTrade` (does not consume webhook fields).
- Changes to manual trading panel UI.
- Risk Source / TP Source overrides for `Dynamic` risk mode (Lite hardcodes Fixed; relevant only when porting to Pro).

---

## Readiness assessment

**READY for `/execute-plan`.** All four BLOCKING devil's-advocate issues incorporated into AC. All SIGNIFICANT items resolved or explicitly deferred. Task granularity is appropriate: each task is bounded to one logical change in one file region, with clear AC. Serial execution removes parallelism risk for the single shared file.

**Before running `/execute-plan`, recommended:** none. Plan is self-contained.

**File:** `D:\iCloud Drive\iCloudDrive\Skyweaver Trading\Bot Codes\Trading Panel New Reversal\docs\plans\webhook-risk-tp-sizing-lite\plan.md`
