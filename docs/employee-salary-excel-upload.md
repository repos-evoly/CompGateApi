# Employee Salary Excel Upload Guide

This guide explains how to upload `SalariesSample.xlsx`-style files and then create salary cycles from imported employees.

## 1) Excel format

Use one worksheet with these columns:

- `Column A`: employee `Name`
- `Column B`: `Account Number` (must be exactly 13 digits)
- `Column C`: `Salary`

The first row can be a header (`Name`, `Account Number`, `Salary`) or data.

## 2) Upload from Swagger

1. Run API and open Swagger UI.
2. Authorize with a **company user** token (policy: `RequireCompanyUser`).
3. Open endpoint: `POST /api/employees/upload`.
4. Click `Try it out`.
5. In `files`, select your `.xlsx` file.
6. Click `Execute`.

Response returns summary:

- `totalRows`
- `createdCount`
- `updatedCount`
- `skippedCount`
- `errors` with row numbers (if any invalid rows were skipped)

Import behavior:

- Insert-only by account number inside the authenticated company.
- New employees are created.
- Existing employees with the same account number are skipped (not updated).
- Imported employees are set with:
  - `accountType = "account"`
  - `sendSalary = true`
  - `canPost = true`

## 3) Verify imported employees

Use:

- `GET /api/employees`

Imported employees should appear there immediately.

## 4) Create salary cycle using imported employees

Use:

- `POST /api/employees/salarycycles`

If you do **not** pass explicit `entries`, the cycle includes company employees with `sendSalary = true` (including imported rows).

Example request body:

```json
{
  "salaryMonth": "2026-04",
  "additionalMonth": null,
  "debitAccount": "1234567890123",
  "currency": "LYD"
}
```

Then post the cycle with:

- `POST /api/employees/salarycycles/{id}/post`
