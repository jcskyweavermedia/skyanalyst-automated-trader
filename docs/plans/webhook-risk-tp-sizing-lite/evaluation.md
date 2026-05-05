# Evaluation — webhook-risk-tp-sizing-lite

**Verdict:** ✅ Implementation complete; **smoke testing required** before declaring milestone closed.

---

## Task completion

| Task | Status | Verified |
|---|---|---|
| T1  | [x] Done | `enum RiskTPSource { App, Bot }` added at L119 |
| T2  | [x] Done | 5 new properties on `WebhookData` at L199-214 with `[JsonPropertyName]` attributes |
| T3  | [x] Done | 5 lines added inside `new WebhookPayload { ... }` initializer (~L2179-2183); `Confidence` line gained trailing comma |
| T4  | [x] Done | 3-clause validation block inserted in `ValidateWebhook` before `return true` |
| T5  | [x] Done | `RiskSource` and `TpSource` `[Parameter]` props at L496-500, both default `Bot`, Group "Trading Bridge Settings" |
| T6  | [x] Done | Method signature now takes `riskAmount`, `tp1Percent`, `tp2Percent`, `tp3Percent`. Internal `CalculateRiskAmount` call removed. Single caller updated to pass bot-derived values. Caller's duplicate `riskAmount` declaration moved up |
| T7  | [x] Done | `CalculateWebhookRiskAmount(WebhookData)` helper added between `CalculateFixedRiskAmount` and `CalculateDynamicRiskAmount` |
| T8  | [x] Done | `effectiveRiskAmount` ternary computed before `CalculateWebhookPositionSizes` call. Falls back to `CalculateRiskAmount` when `RiskSource == Bot` or `webhook.Risk` is null |
| T9  | [x] Done | `effectiveTp1/2/3Percent` ternaries computed BEFORE `hasTP2`/`hasTP3` derivation. `hasTP2`/`hasTP3` rewritten to use effective values. Effective values passed to `CalculateWebhookPositionSizes` |
| T10 | [x] Done | Directional `slSign`/`tpSign` removed. Uniform `slPrice/tp1/tp2/tp3 += offsetPrice`. "widen" comment removed. Log text updated to "uniform shift" |
| T11 | [x] Done | Success log replaced with `Risk[<source>]: $<amount>, TP[<source>]: <p1>/<p2>/<p3>`. Unused `riskPercent` declaration removed |

## Devil's-advocate blockers — all resolved

| # | Issue | Resolution verified |
|---|---|---|
| B1 | `CalculateRiskAmount` call inside helper would override App risk | T6 removed the internal call; T8 passes `effectiveRiskAmount` ✅ |
| B2 | T8/T9 needed transitive deps on T2+T3 | T3 explicitly added the 5 initializer lines; new fields now flow through to `ExecuteWebhookTrade` ✅ |
| B3 | `hasTP2`/`hasTP3` derivation read bot fields | T9 rewrote to use `effectiveTp2Percent`/`effectiveTp3Percent` BEFORE the derivation, fixing the ordering ✅ |
| B4 | Wave A parallelism unsafe | Executed serially as recommended ✅ |

## Static checks performed

- `Grep` confirmed only ONE caller of `CalculateWebhookPositionSizes` exists (the one in `ExecuteWebhookTrade`).
- `Grep` confirmed no orphaned references to removed identifiers (`slSign`, `tpSign`, old `riskPercent:F2` log fragment).
- `using System;` provides `StringComparison.OrdinalIgnoreCase` — no missing imports.
- All new identifiers (`RiskTPSource`, `RiskSource`, `TpSource`, `Risk`, `RiskUnit`, `Tp1Percent`/`Tp2Percent`/`Tp3Percent`, `CalculateWebhookRiskAmount`, `effectiveRiskAmount`, `effectiveTp1Percent`/`effectiveTp2Percent`/`effectiveTp3Percent`) consistent with declared spelling.
- Manual walkthrough of `ExecuteWebhookTrade` confirms data flow: validate → offset shift → effective TPs → `hasTP2`/`hasTP3` from effective → effective risk → sizing → execute → log.

## What is NOT verified by static review

- **Compile against cAlgo SDK** — needs to happen in cTrader IDE. The user must open the file in cAlgo and confirm a clean build before any smoke testing.
- **Runtime behavior** — the 9 smoke-test cases enumerated in the plan must all pass.
- **No regressions** in existing flows (manual trades, hard stops, position closure, day rollover, port retry) — these code paths weren't modified, but a full run-through is prudent.

## Recommendation

**Pause execution. Smoke test required.**

The user should:
1. Open `SkyAnalyst Automated Trader Lite.cs` in cTrader's cAlgo IDE.
2. Verify a clean build (no errors, no new warnings).
3. Run the 9 smoke tests from the plan's "Acceptance criteria (feature-level)" section.
4. Report any failure → return to bug-hunter; re-test after any fix.
5. Once all 9 pass → mark feature done and proceed to a new plan for the Pro variant + port-hardening propagation.

## Next workflow stage

`pause` — awaiting user smoke test results before proceeding to Pro variant work.
