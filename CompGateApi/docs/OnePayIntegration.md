# OnePay Transfer Integration

## Workflow

1. A maker with `canCreateTransfer` selects a company source account, a OnePay-enabled institution, the destination account, amount, and narrative.
2. `POST /api/onepay/transfers/validate` calls the provider's `ValidateOutgoingTransfer` endpoint and returns the verified destination account name plus a short-lived validation token.
3. After the maker confirms the verified name, `POST /api/onepay/transfers` consumes that token once and creates a `PendingApproval` transfer.
4. A user with `canposttransfer` calls `POST /api/onepay/transfers/{id}/approve`. The permission allows the user to approve a pending transfer even when they created it. The backend atomically claims the transfer and sends `ExecuteOutgoingTransfer` once.
5. A final provider response produces `Completed` or `Failed`. A still-processing response produces `PendingProviderResponse`.
6. The reconciliation worker calls only `CheckTransactionStatus`. After three unresolved automatic checks, it changes the transfer to `Unknown`; manual refresh can still resolve an unknown transfer later.

The public statuses are exactly:

- `PendingApproval`
- `PendingProviderResponse`
- `Completed`
- `Failed`
- `Unknown`

There is no draft, delete, discard, beneficiary, edit, or automatic execute retry flow.

## Configuration

The UAT base URL and system ID have non-secret defaults in `OnePayOptions`. The checksum password must be supplied at runtime and must not be committed:

```sh
export OnePay__ChecksumPassword='<provided-UAT-password>'
```

All supported overrides use standard ASP.NET Core configuration keys:

```text
OnePay__BaseUrl
OnePay__SystemId
OnePay__ChecksumPassword
OnePay__TimeoutSeconds
OnePay__ValidationSessionMinutes
OnePay__InstitutionCacheMinutes
OnePayReconciliation__Enabled
OnePayReconciliation__PollIntervalSeconds
OnePayReconciliation__RetryDelaySeconds
OnePayReconciliation__MaxStatusChecks
OnePayReconciliation__ClaimLeaseSeconds
OnePayReconciliation__BatchSize
```

`OnePayReconciliation__MaxStatusChecks` defaults to `3`. The worker never calls the execute API.

## Permission Prerequisites

Before UAT, verify that the environment already contains and assigns these exact functional permission names:

- `canCreateTransfer` for makers.
- `canposttransfer` for checkers.

They are intentionally not inserted by the feature migration because permission IDs and role assignments are environment-owned data. `CompanyCanTransfer` controls sidebar visibility only and does not authorize either financial action.

## Database

Apply migrations `20260806112742_AddOnePayTransfers` and `20260806113835_AddOnePayReconciliationIndex` before enabling the feature. They create:

- `OnePayValidationSessions`: short-lived, single-use provider validation results.
- `OnePayTransfers`: transfer snapshots, maker/checker audit fields, provider references, statuses, and reconciliation state.

## API Surface

```text
GET  /api/onepay/accounts
GET  /api/onepay/institutions?language=EN
GET  /api/onepay/transfers?page=1&limit=10&searchTerm=
GET  /api/onepay/transfers/{id}
POST /api/onepay/transfers/validate
POST /api/onepay/transfers
POST /api/onepay/transfers/{id}/approve
POST /api/onepay/transfers/{id}/refresh-status
```

Provider references and the checksum password remain backend-only. The device ID is accepted only from the trusted authentication cookie forwarded by the frontend proxy; browser latitude and longitude are optional.
