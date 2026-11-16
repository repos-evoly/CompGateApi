# EUR transfer commission account

The global `Settings.CommissionAccountEUR` account receives EUR transfer fees. It is independent of the LYD and USD collection accounts and applies to all companies in the environment. It does not change the service-package fee rates or the company's sender/recipient fee-payer setting. Salary fee accounts continue to use their existing Pricing/GL rules.

## API and CMS behavior

- `GET /api/settings` includes `commissionAccountEUR`; `PATCH /api/settings` accepts it under the existing admin authorization policy.
- The CMS Settings page has an English/Arabic EUR account input with a `0000-000000-000` display mask. The API accepts formatted or unformatted input, removes spaces and hyphens, and stores exactly 13 ASCII digits including leading zeros.
- Omitting the PATCH field preserves the account. An explicit empty string clears it; null is treated as omitted. A missing EUR account does not prevent editing other settings.
- Both direct transfer posting and posting an existing draft select the EUR collection account for EUR. A payable EUR fee with a missing/invalid account is rejected before the financial posting request. No-fee transfers keep the second leg disabled. LYD/USD account selection is unchanged.
- The dashboard adds `currencyTotals` entries containing `currencyCode`, `accountNumber`, and `value` for LYD, USD, and EUR. Existing `lydValue`, `otherCurrenciesValue`, and `accountNumbers` remain available for older clients. The new CMS shows separate currency cards; against an older API it labels the combined non-LYD amount as foreign currencies, not USD.
- The salary commission card now uses the salary title instead of the USD transfer title. English LYD/USD dashboard labels are also supplied.
- Transfer commissions and statement service fees count only `Completed` transfer records. Pending draft commissions are estimates and are excluded, as are other uncompleted statuses. The matching transfer count also excludes uncompleted records. Existing date filtering and stored commission amounts are preserved; salary commissions retain their posted-cycle/transferred-entry rules.
- The account labels show the currently configured accounts; the card values are collected-commission summaries, not bank balance queries or a historical account-routing ledger. No historical transfer is reposted or moved by this feature. This reporting correction requires no migration or edits to existing transfer records.

## Migration and current rollout

Migration: `20261109095055_AddEurCommissionAccount`.

The migration adds only the nullable `Settings.CommissionAccountEUR` (`nvarchar(13)`) column. It does not seed an account for every environment, change other tables, or change LYD/USD settings.

On 2026-11-09, this single migration was applied to **UAT `10.3.3.11` / `CompGateDB`**, and the EUR account was configured as `0010932701978` (display: `0010-932701-978`). The existing settings and unrelated migration history were compared within the transaction and preserved. A fresh connection verified the column, migration record, and configured account.

On 2026-11-16, the same reviewed migration was applied to **production `10.1.1.205` / `CompGateDB`**, adding only the nullable column and its EF migration-history record. Existing settings and unrelated migration history were compared inside the transaction and preserved. A fresh connection verified exactly one migration record and the expected nullable `nvarchar(13)` column. The production EUR account value was left `NULL`; configure the production account through the updated CMS after application deployment.

No application deployment was performed as part of the production migration. The updated CompGateApi, compgateway-admin-cms, and compgateway2 releases are still required to apply all EUR changes and the latest reporting/display corrections. Older release packages predate those corrections.

Reviewed SQL, UAT backup, and result records: workspace `output/eur-commission/`. The UAT-only helper is `tmp/eur-commission-uat.ps1` (read-only by default; already applied with `-Apply`). Do not run its apply mode again. It pins the UAT connection and reviewed SQL hash and has no production target.

Production snapshot/result records are in the same workspace directory. The separate `tmp/eur-commission-production.ps1` helper pins the production connection and SQL hash, defaults to verification mode, and applies only this migration when explicitly invoked with `-Apply`. Production apply mode has already completed; use verification mode for later checks.

The EF scaffold initially included unrelated pre-existing snapshot/model differences. The final migration and its target snapshot were restricted to the new column while preserving the previous snapshot and employee index. Do not apply unrelated migrations as part of this feature; use the reviewed single-migration script.

## Verification

- All 63 CompGateApi tests passed, including currency routing in both posting paths, sender/recipient charging, missing/invalid EUR account rejection without financial posting, no-fee EUR posting, settings validation/mapping, dashboard currency/date separation, and single-column migration shape. Five dashboard regressions cover pending EUR drafts, exclusion of uncompleted statuses across currencies, completion transitions, date boundaries, and uncompleted statement fees.
- All 18 CMS tests passed, including independent EUR/USD values and account labels, response order independence, salary labels, older API fallback, and English/Arabic labels.
- CMS TypeScript check and ESLint on the changed settings/dashboard modules passed.
- No real bank transfer was submitted; posting tests use an in-memory database and simulated bank responses.
