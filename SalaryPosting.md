# Salary Posting — Current Accounting Flow

This document explains what the system does today when posting salaries: validations, transfer steps, exact accounting entries, and who receives the commission. It reflects the current implementation in `CompGateApi.Core/Repositories/EmployeeSalaryRepository.cs`.

## What Happens (Summary)
- For each eligible employee, the system transfers the employee’s net salary (gross − fixed fee) from the company debit account to the employee account.
- After the employee transfers, the system posts one aggregated fee transfer (sum of all per‑employee fixed fees for successful employee transfers) from the same company debit account to a fee GL account.
- Net effect:
  - Employees receive net salary (they “pay” the fee because it’s deducted from their gross).
  - The fee GL is credited with the total fee; this is the bank’s income account.
  - The company’s total outflow equals the sum of gross salaries for the successfully posted employees.

## Preconditions and Validations
- Service package must enable the Transaction Category "Salary Payment" for the company.
- Each included employee must have:
  - `AccountType = "account"` (wallets are not used for salary here), and
  - a 13‑digit `AccountNumber`.
- Payroll cycle debit account must be 13 digits.
- Fixed fee pricing must exist in `Pricing` table with `TrxCatId = 17` and `Unit = 1` and `Price > 0`.
- For each employee, `salary >= fixedFee`.
- Fee GL must be resolvable to a 13‑digit account via:
  - `Pricing.GL1` (preferred), or
  - derived from the company debit account as `{BRANCH}{932702}{CCY3}` where `BRANCH` is the first 4 digits and `CCY3` is the last 3 digits (currency code) of the debit account.

## Posting Steps
1) Build and post the Employee Transfers (net amounts)
   - For each eligible employee:
     - Compute `employeeCredit = salary − fixedFee`.
     - Add a line to debit the company account and credit the employee account by `employeeCredit`.
   - Send a bulk transfer request to the bank (`POST /api/mobile/PostGroupTransfer`).
   - Parse the response and mark only the successfully credited employee accounts. Totals are computed based on successes only.

2) Settle the Aggregated Fee
   - Compute `totalFee = numberOfSuccessfulEmployees × fixedFee`.
   - Post one transfer debiting the same company debit account and crediting the fee GL by `totalFee`.
   - Bank call is the same group transfer endpoint, with a single line.

3) Persist Results
   - Save bank reference IDs for the employee batch and fee batch, and the raw responses.
   - Mark successful entries as transferred with timestamps and the posting user.
   - Update the cycle’s `TotalAmount` to the sum of the gross salaries of the successful employees.

## Accounting Entries
- Per employee (on success):
  - Dr Company Debit Account = net salary (`gross − fixedFee`)
  - Cr Employee Account = net salary

- Aggregated fee (once per batch, for all successes):
  - Dr Company Debit Account = `numberOfSuccessfulEmployees × fixedFee`
  - Cr Fee GL Account = same amount

- Combined effect for the company:
  - Total Dr Company = sum of gross salaries of successful employees
  - Credits go to employees (net) and fee GL (total fee)

## Who Pays and Who Receives the Commission
- Payer of the fee: The employee (receiver) effectively pays the fee because their credited amount is reduced by the fixed fee.
- Recipient of the fee: The bank’s fee GL account (either `Pricing.GL1` or the derived `{BRANCH}932702{CCY3}`) receives the fee as income.
- Company’s position: The company funds the gross amount overall (net to employees + total fee to fee GL), so the company’s total outflow equals the sum of gross salaries.

## Fee GL Determination
- Preferred from configuration: `Pricing.GL1` (must be a 13‑digit account).
- Fallback (derived from debit account): `{BRANCH}932702{CCY3}`
  - Example: debit `1234098765001` → branch `1234`, ccy `001` → fee GL `1234932702001`.

## Bank API Call (shape used)
- Named client: `BankApi`
- Endpoint: `POST /api/mobile/PostGroupTransfer`
- For each group transfer line the payload includes:
  - `YBCD06DACC` = company debit (13 digits)
  - `YBCD06CACC` = credit account (employee or fee GL)
  - `YBCD06AMT` and `YBCD06AMTC` = amount as zero‑padded 15‑digit integer of minor units (3 decimals used)
  - Optional narrations: fee batch uses a narration from `Pricing.NR2` or defaults to "Salary receiver‑paid fixed fee"

## Worked Example
Assume:
- Company debit: `1234098765001` (branch=`1234`, ccy=`001` for LYD)
- Employees: A=`5678123456701` gross `1,000.000 LYD`, B=`5678123456702` gross `1,500.000 LYD`
- Fixed fee: `2.000 LYD` (TrxCatId=17, Unit=1)
- Fee GL (derived): `1234932702001`

Entries
- Employee transfers (net):
  - A: Dr Company 998.000 / Cr A 998.000
  - B: Dr Company 1498.000 / Cr B 1498.000
- Aggregated fee:
  - Dr Company 4.000 / Cr Fee GL 4.000

Totals
- Company total debit: `998.000 + 1498.000 + 4.000 = 2,500.000 LYD` (sum of gross)
- Employees total credit: `998.000 + 1498.000 = 2,496.000 LYD`
- Fee GL credit: `4.000 LYD`

## Where This Lives in Code
- Repository: `CompGateApi.Core/Repositories/EmployeeSalaryRepository.cs`
  - Employee batch build and post; fee batch build and post; GL derivation helper.
- Models: `CompGateApi.Data/Models/SalaryCycle.cs`, `CompGateApi.Data/Models/SalaryEntry.cs`, `CompGateApi.Data/Models/Employees.cs`, `CompGateApi.Data/Models/Pricing.cs`
- DTOs: `CompGateApi.Core/Dtos/EmployeeSalaryDto.cs`

If you want this behavior changed (e.g., make the company pay the fee instead of the employee, or change the GL), let me know and I can outline the minimal code changes required.

