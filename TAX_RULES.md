# TaxNavi tax rules — validation register

All rates, thresholds, reliefs and deadlines live in **`backend/src/Api/appsettings.json` → `TaxRules`**.
The engine (`NigerianTaxCalculationEngine`) contains no hard-coded rates, and the AI assistant builds its answers only from this configuration.

Each rule set covers a range of tax years and carries a `VerificationStatus`. Users see that status as a badge in the app.

| Status | Meaning | Shown to users as |
|---|---|---|
| `Verified` | Checked against gazetted legislation or NRS guidance | "Verified" |
| `SecondarySourcesVerified` | Consistent across professional adviser summaries (KPMG, EY, PwC); statute text not yet checked line by line | "Source-checked" |
| `PendingValidation` | Sources conflict or are unverified | "Pending validation", with a caution |

## Rule set `nta-2025` — tax years 2026 onward (Nigeria Tax Act 2025, effective 1 Jan 2026)

| Rule | Value used | Status | Action needed |
|---|---|---|---|
| PIT bands | 0% to ₦800k · 15% to ₦3m · 18% to ₦12m · 21% to ₦25m · 23% to ₦50m · 25% above | Source-checked | Confirm against the Fourth Schedule |
| CRA | Abolished | Source-checked | — |
| Rent relief | 20% of annual rent, capped at ₦500,000 | Source-checked | Confirm s.30(2)(a)(vi) wording |
| Eligible deductions | Pension, NHF, NHIS, life assurance/annuity, owner-occupied mortgage interest | Source-checked | — |
| Small company | Turnover ≤ ₦100m **and** fixed assets ≤ ₦250m; professional services excluded; 0% CIT | **Pending** | Sources cite ₦50m (Act text) vs ₦100m (later guidance). Confirm with NRS |
| CIT (other companies) | 30% + 4% development levy (small companies exempt from levy) | **Pending** | Confirm the rate and any medium-company tier |
| VAT | 7.5%; small-business exemption at turnover ≤ ₦100m | **Pending** | Sources cite ₦25m / ₦50m / ₦100m. Confirm with NRS |
| Final-tax investment income | Excluded from chargeable income when the user marks WHT as final | Engine assumption | Confirm treatment of dividends and interest under the NTA |
| Deadlines | PAYE by 10th · VAT by 21st · PIT return by 31 Mar · CIT 6 months after year-end · employer return 31 Jan | Mixed (see config) | Confirm against the NTAA 2025 |

## Rule set `pita-legacy` — tax years 2024–2025 (repealed laws, still apply to those years)

| Rule | Value used | Status | Action needed |
|---|---|---|---|
| PIT bands | 7 / 11 / 15 / 19 / 21 / 24% | Established | — |
| CRA | Higher of ₦200k or 1% of income, plus 20% | **Pending** | Finance Act 2020 may require the CRA base to exclude pension/NHF/NHIS. TaxNavi currently computes CRA on chargeable income (business profit, not receipts) |
| CIT | Small ≤ ₦25m 0% · medium ≤ ₦100m 20% · others 30% · TET 3% | **Pending** | Confirm the TET rate per year |
| VAT | 7.5%; exempt below ₦25m | Source-checked | — |
| Minimum tax | Not modelled | — | — |

## Not modelled — users are sent to a professional

Capital allowances, losses carried forward, minimum ETR for large groups, capital gains, foreign income and residency, penalties, and disputes.

## Updating rules

1. Edit the rule set in `appsettings.json`, or add a new one with `FirstTaxYear` set to the start year.
2. Update `VerificationStatus` and `Sources`.
3. Run `dotnet test`. The tests load the shipped `appsettings.json`, so band and threshold changes must be reflected in `TaxCalculationEngineTests.cs`.
