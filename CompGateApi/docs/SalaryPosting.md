# Salary Posting Flow

This document describes how salary posting works in CompGateApi: which accounts are debited/credited, the bank APIs called, validations, and a small numeric example.

## Overview
- Feature surface:
  - Create salary cycle: `POST /api/employees/salarycycles`
  - Post salary cycle (execute transfers): `POST /api/employees/salarycycles/{id}/post`
- Implementation:
  - Endpoint: `CompGateApi/Endpoints/EmployeeSalaryEndpoints.cs`
  - Repository: `CompGateApi.Core/Repositories/EmployeeSalaryRepository.cs`
  - Exceptions: `CompGateApi.Core/Errors/PayrollException.cs`
- External calls use the named HttpClient `BankApi` (configured in DI) with base address `http://10.1.1.205:7070`.

## Validations and Preconditions
- Service package must have Transaction Category "Salary Payment" enabled.
- Employee entries included must:
  - Have `AccountType = "account"` and
  - Have a 13‑digit `AccountNumber`.
- Debit account (payer account) in cycle must be 13 digits.
- Pricing must exist for fixed fee: `Pricings` row with `TrxCatId = 17` and `Unit = 1`.
  - `Price` must be > 0.
- For each employee, `salary >= fixedFee`.
- Fee GL account must be valid:
  - Preferred: `Pricings.GL1` (13 digits).
  - Fallback: derived from the debit account as `{BRANCH}{932702}{CCY3}` where:
    - `BRANCH` = first 4 digits of debit account
    - `CCY3` = last 3 digits of debit account (e.g., `001` for LYD)

## Posting Steps
1) Build the "employee net transfers" batch
- For each eligible employee:
  - Compute `employeeCredit = salary - fixedFee` (net amount to employee).
  - Add a group item that debits the company’s debit account and credits the employee’s 13‑digit account by `employeeCredit`.
- Build request payload with header `HID = <timestamp> + "GT00"` and a `GroupAccounts` array of the items.
- Call Bank API: `POST /api/mobile/PostGroupTransfer` using `BankApi`.
- Parse response to mark which employee accounts succeeded; only successes are considered in totals.

2) Settle the total fee in a separate batch
- Compute `totalFee = (#successfulEmployees) * fixedFee`.
- Build a single group item transferring `totalFee` from the same company debit account to the fee GL account.
  - Fee GL comes from `Pricings.GL1` or derived as `{BRANCH}932702{CCY3}`.
  - Fee narration uses `Pricings.NR2` or defaults to "Salary receiver-paid fixed fee".
- Build request payload with `HID = <timestamp> + "GF00"` and the single fee item.
- Call Bank API: `POST /api/mobile/PostGroupTransfer` using `BankApi`.

3) Persistence
- Salary cycle stores:
  - `BankReference` for the employees batch.
  - `BankFeeReference` for the fee settlement batch.
  - Raw responses for audit.
  - Each successful entry is marked `IsTransferred = true` with timestamps and user.
- `TotalAmount` on the cycle reflects the sum of gross amounts for successful employees.

## Accounts and Accounting
- Company debit account (payer):
  - Debited by the sum of net salaries in step 1.
  - Debited by the total fee in step 2.
  - Total debit = sum of gross salaries of successful employees.
- Employee accounts (receivers):
  - Credited by their net salary: `gross − fixedFee` (per employee).
- Fee GL account:
  - Credited by the aggregated total fee (`#success × fixedFee`).

Notes
- Functionally, the employee "pays" the fee in the sense they receive `gross − fee`. Operationally, the company’s account funds both the net to employees and the fee settlement to the GL, so the company’s total outflow equals the gross sum.

## Bank API Details
- Named client: `BankApi` (DI)
- Endpoint: `POST /api/mobile/PostGroupTransfer`
- Payload (shape used):
  - Header
    - `system = "MOBILE"`
    - `referenceId = HID` (e.g., `yyyyMMddHHmm + GT00` or `GF00`)
    - `userName = "TEDMOB"`
    - `customerNumber = debitAccount.Substring(4, 6)`
    - `requestTime = ISO‑8601`
    - `language = "AR"`
  - Details
    - `@HID = HID`
    - `@APPLY = "Y"`
    - `@APPLYALL = "N"`
    - `GroupAccounts = [ ... ]`
      - Each item fields (strings, 15‑digit zero‑padded amounts):
        - `YBCD06DID` = detail id (e.g., `yyyyMMddHHmm + GT01`)
        - `YBCD06DACC` = debit account (13 digits)
        - `YBCD06CACC` = credit account (employee or fee GL)
        - `YBCD06AMT`  = amount (e.g., net salary or total fee)
        - `YBCD06CCY`  = currency code
        - `YBCD06AMTC` = same amount (copy)
        - `YBCD06COMA` = `"000000000000000"`
        - `YBCD06CNR3`, `YBCD06DNR2` = narration strings (fee uses `DNR2`)

## Example
Assumptions
- Debit (company) account: `1234098765001` (13 digits; branch=`1234`, ccy=`001` for LYD)
- Employees and gross salaries:
  - Employee A: account `5678123456701`, gross `1,000.000 LYD`
  - Employee B: account `5678123456702`, gross `1,500.000 LYD`
- Fixed fee (TrxCatId=17, Unit=1): `2.000 LYD` per employee
- Fee GL:
  - From pricing `GL1` if set; else derived: `1234 932702 001` → `1234932702001`

Step 1: Employee transfers (net)
- Employee A credit = `1000.000 − 2.000 = 998.000 LYD`
- Employee B credit = `1500.000 − 2.000 = 1498.000 LYD`
- Company debits:
  - `998.000 + 1498.000 = 2,496.000 LYD`
- Employees credited:
  - A: `998.000` to `5678123456701`
  - B: `1498.000` to `5678123456702`

Step 2: Fee settlement (aggregated)
- Successful employees: 2 → `totalFee = 2 × 2.000 = 4.000 LYD`
- Company debits: `4.000 LYD`
- Fee GL credited: `4.000 LYD` to `1234932702001`

Totals
- Company total debit: `2,496.000 + 4.000 = 2,500.000 LYD` (sum of gross)
- Employees total credit: `2,496.000 LYD` (sum of net)
- Fee GL total credit: `4.000 LYD`

## Error Handling (common)
- `PayrollException` is thrown on:
  - Salary feature not enabled in service package.
  - No eligible employees (must be 13‑digit account type).
  - Pricing missing or invalid (fixed fee ≤ 0).
  - Debit account not 13 digits.
  - Fee GL invalid (neither GL1 nor derived is valid 13‑digit).
  - Bank API rejects or returns non‑success.
- On partial success, only successful employees are marked and included in totals; fee settlement uses only successful count.

## Code References
- `CompGateApi.Core/Repositories/EmployeeSalaryRepository.cs`
  - Employee batch: build payload around lines ~420‑470
  - Bank POST: `PostGroupTransfer` (employees) and then again for fee
  - Fee GL derivation helper: `BuildCommissionGlFromSender`
- `CompGateApi/Endpoints/EmployeeSalaryEndpoints.cs`
  - `POST /api/employees/salarycycles/{id}/post` → posts a cycle

