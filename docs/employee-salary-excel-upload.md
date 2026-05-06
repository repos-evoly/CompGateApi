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
- `deletedCount`
- `skippedCount`
- `errors` with row numbers (if any invalid rows were skipped)

Import behavior:

- Sync by account number inside the authenticated company.
- New employees are created.
- Existing employees with the same account number are updated from Excel (`Name` and `Salary`) and reactivated if previously soft-deleted.
- Existing employees keep their current `Email` and `Phone` values because these columns are not included in the Excel file.
- Existing employees missing from the uploaded Excel are marked with `isDeleted = true`.
- Imported employees are set with:
  - `email = null`
  - `phone = null`
  - `accountType = "account"`
  - `sendSalary = true`
  - `canPost = true`
  - `isDeleted = false`

## 3) Verify imported employees

Use:

- `GET /api/employees`

Imported employees should appear there immediately.
Employees marked with `isDeleted = true` are not returned by this endpoint.

## 4) Create salary cycle using imported employees

Use:

- `POST /api/employees/salarycycles`

If you do **not** pass explicit `entries`, the cycle includes active company employees with `sendSalary = true` (including imported rows). Soft-deleted employees are not included and cannot be posted.

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
