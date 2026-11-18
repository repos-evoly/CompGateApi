# OnePay and LYPay transfer release

This release enables OnePay and LYPay transfers on the current main branch without
merging the employee wallet-salary implementation.

## Included

- Existing OnePay and LYPay endpoints, provider clients, transfer services and
  reconciliation workers are registered again.
- LYPay execution uses canonical decimal serialization when generating the checksum.
- Beneficiaries support Normal, OnePay and LYPay rails, institution validation,
  company ownership, maker/checker permissions, active-destination uniqueness and
  row-version concurrency. Existing ordinary-beneficiary update requests can omit
  the row version; a supplied version is still checked. Rail-beneficiary updates
  require a valid row version.
- The company website includes transfer list, validation, creation, approval and
  status screens, payment-service beneficiaries, sidebar links and translations.
- Both integrations read the same `OnePay` configuration section, including its
  BaseUrl, SystemId and ChecksumPassword. Environment-specific values belong in
  appsettings.Staging.json and appsettings.Production.json or deployment overrides.
  These private configuration files are not committed to Git.

## Salary exclusion and existing fixes

No salary or employee source, wallet-salary services/workers, salary-allocation
models, salary migrations or EF migration snapshots are changed in this release.
The existing main-branch salary PDF, employee Excel, EUR commission, authentication,
mobile-access and notification fixes remain present.

## Database preflight before deployment

Re-enabling the rail-aware beneficiary model requires the existing payment-rail
schema. This code merge does not execute a migration or modify any database.
Check migration history and the actual schema in each target environment before
deploying. The relevant existing migrations are:

- `20260806112742_AddOnePayTransfers`
- `20260806113835_AddOnePayReconciliationIndex`
- `20260807113656_AddLyPayTransfers`
- `20260810120000_AddRailAwareBeneficiaries`
- `20260810130000_EnforceUniqueRailBeneficiaryDestinations`
- `20260810190000_AddOnePayProviderDiagnostics`

In particular, verify the beneficiary PaymentRail, CreatedByUserId, InstitutionId,
ProviderInstitutionReference, InstitutionName and RowVersion columns and the
payment-rail destination index. Use a reviewed script for any missing payment
migrations. This release does not require wallet-salary migrations or an unrestricted
database update.

The OnePay/LYPay reconciliation workers resume checking existing provider transfers
when the API starts. Wallet-salary workers are not registered.

## Validation

The API tests exercise route registration and uniqueness, rail/company isolation,
permissions, ordinary beneficiary create/edit/archive behavior and provider HTTP
payload/checksum handling through a fake HTTP handler. They make no bank/provider
payments. Run the existing website regression tests and optimized Next.js build.
