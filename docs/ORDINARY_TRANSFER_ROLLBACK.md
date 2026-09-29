# Ordinary transfer compatibility rollback

## Scope

The OnePay/LyPay integration merged in c032d90 is disabled at the API and
service-registration boundaries. Its source and historical migrations are
retained, but its endpoint classes are excluded from compilation and its
provider clients, transfer services, and reconciliation workers are not registered.
Calls to /api/onepay and /api/lypay are no longer exposed by this API.
Rail beneficiary requests are rejected rather than silently treated as bank transfers.

Ordinary transfer posting, limits, commissions, approvals, and transaction handling
are unchanged. Mobile authentication, authorization/permissions, notification
outbox, and notification content remain in place. Salary code is unchanged:
feature/wallet-salary-integration was not merged into main.

## Beneficiary compatibility

Ordinary beneficiaries no longer read/write the rail migration columns:
PaymentRail, CreatedByUserId, InstitutionId, ProviderInstitutionReference,
InstitutionName, and RowVersion. Pagination, search, editing, and soft-delete
aliases remain available for the current web application. Company ownership and
deleted-record checks remain enforced. The rail RowVersion requirement is removed;
normal editing uses the legacy behavior without a concurrency token.

A read-only schema check selects the compatible query:

- Legacy production schema: use the original beneficiary columns.
- Upgraded UAT schema: additionally filter PaymentRail = Normal in SQL, so existing
  OnePay/LyPay beneficiary records cannot be listed, edited, deleted, or used as
  ordinary notification recipient labels.

The transfer notification lookup uses this same compatibility query and retains
amount, currency, recipient name, and masked-account fallback behavior.

## Deployment and database safety

Deploy only a newly published CompGate API for this change. No CompAuth API,
mobile API, mobile app, or web build is required by this rollback.
No database migration is required; do not run an unrestricted database update.
Do not delete tables, migration history, or provider transaction/beneficiary records.

Before deploying in an environment that has used OnePay/LyPay, review outstanding
provider transfers. The disabled workers will no longer reconcile them. Settle or
plan their reconciliation separately; do not resubmit them as ordinary transfers.

Historical EF migration snapshots deliberately remain unchanged. The runtime
beneficiary mapping is now a legacy-compatible subset. Do not scaffold/apply a
migration that drops ignored rail columns as a consequence of this rollback.
Any future schema migration or re-enabling of the integration needs explicit review
of that model/snapshot difference.

## Verification

Run:

```powershell
dotnet test .\CompGateApi.Tests\CompGateApi.Tests.csproj -c Release
```

Tests cover legacy/upgraded query shapes, intercepted notification lookups with
no real SQL or bank connection, excluded integration routes/workers, rail rejection,
ordinary create/edit/archive, company isolation, and existing notification tests.

Before production acceptance, smoke-test an ordinary transfer through the web app
in UAT: creation, approval, posting, and mobile notification delivery. Automated
tests do not execute financial transactions against a live bank.
