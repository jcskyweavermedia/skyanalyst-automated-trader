# Security Review — webhook-risk-tp-sizing-lite

**Scope:** Changes to `SkyAnalyst Automated Trader Lite.cs` only (T1-T11 from plan).
**Reviewer:** assistant (devil's-advocate framing, OWASP-style pass).
**Verdict:** ✅ No blocking issues. One minor note.

---

## Threat surface added by this work

The new webhook fields (`risk`, `risk_unit`, `tp1_percent`, `tp2_percent`, `tp3_percent`) come from a network source — the local HTTP listener (`http://localhost:<port>/webhook/`). The listener already binds to localhost only, so the threat model is constrained to processes on the same host.

Each new field is one of:
- `double?` — primitive numeric, deserialized via `System.Text.Json` (safe; no script execution surface).
- `string` (`RiskUnit`) — used in case-insensitive equality comparisons only; never concatenated into a shell command, file path, SQL, regex, or HTML.

## Findings

### ✅ T4 — Input validation correct
- All three rejection cases use the existing `errorMessage` out-pattern; `ValidateWebhook` is the single chokepoint upstream of `ExecuteWebhookTrade`. No bypass path.
- `RiskUnit` comparison uses `StringComparison.OrdinalIgnoreCase` — matches T7 helper and is the safe default.
- `webhook.Risk.Value <= 0` correctly rejects 0 and negatives. NaN/Infinity cannot survive `System.Text.Json` deserialization into `double?` (throws / yields null) — no need to additionally guard.
- Error message echoes the user-supplied `RiskUnit` value (`got '<value>'`). Bounded length (network-side payload size limits exist in the HttpListener); echoed back only into the bot's own log file, not to the network response. Acceptable — useful for debugging, no XSS/log-injection risk in this offline log context.

### ✅ T7 — Helper assumes pre-validation, correctly
- `CalculateWebhookRiskAmount` reads `webhook.Risk.Value` and `webhook.RiskUnit` without null guards.
- T4 always runs before this helper is reachable (the helper is only called from inside `ExecuteWebhookTrade`'s ternary, which gates on `RiskSource == App && webhook.Risk.HasValue`; T4 has already cleared `RiskUnit` for any payload that reaches `ExecuteWebhookTrade` with `Risk.HasValue`).
- Confirmed no other callers of the helper.
- Bounded output: percent path is `Account.Balance * risk / 100` — risk capped by validation, balance is a broker-supplied scalar. Dollar path is `risk` raw. No overflow risk for realistic values.

### ✅ T8/T9 — Wiring respects `RiskSource == Bot` short-circuit
- The ternary evaluates `RiskSource == RiskTPSource.App` first. When `Bot` is selected (default), `webhook.Risk` / `webhook.Tp*Percent` are never read. A malformed payload accepted past T4 cannot influence behavior in `Bot` mode.

### ✅ T10 — Offset arithmetic is bounded
- `SLTPOffsetPips` is a bot-side `[Parameter]` set by the user; not network-supplied. No injection surface.
- Math operates on `double` price values. Negative inputs produce smaller prices (correct, matches user's broker-shift intent). No path produces a negative or zero `slPrice` *within* the offset block alone (still constrained by the `slPrice <= 0 || tp1Price <= 0` guard at the next line).

### ⚠️ Minor — `RiskUnit` deserializes as `null` for nullable-string field
The `RiskUnit` property is declared `public string RiskUnit { get; set; }` (no `?` suffix). cAlgo's compile environment may not have nullable reference types enabled; this works fine. **No action required**, but if the project later enables nullable reference types globally, change to `public string? RiskUnit { get; set; }` for consistency. Not a security issue — just future maintenance hygiene.

## Risks NOT in scope

- The HTTP listener itself, its rate limiting, and concurrent-request handling were not touched by this work — they retain whatever properties the existing port-hardening provides.
- The `SendJsonMessage` broadcast (egress to receiver bots) was explicitly out of scope; it does not include the new fields.
- No new secrets, credentials, or PII are introduced.

## Conclusion

The work introduces validated, narrowly-scoped network input fields. Validation is centralized in `ValidateWebhook`, follows the existing pattern, and uses `OrdinalIgnoreCase` consistently. No SQL, no shell, no auth, no RLS surface relevant to this codebase (single-account local cBot). Cleared for smoke testing.
