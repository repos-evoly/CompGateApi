# Salary Posting — Proposed Accounting Flow (Gross First, Per-Employee Fee)

You asked to change the accounting so employees are credited with the full gross salary first, and then a separate transaction removes the fee from each employee individually. Below is the desired flow, followed by how it differs from the current implementation and what would need to change in code.

## Desired Behavior (Summary)
- Batch 1: Credit each employee the full gross salary; debit the company once for the total gross.
- Batch 2: For each successful employee, create a separate transfer that debits the employee’s account by the fixed fee and credits the fee GL account by the same amount.
- Net effect visible to employees: one incoming full salary, then one outgoing fee debit (per employee). Company outflow still equals the sum of gross salaries.

## Preconditions and Validations
- Service package must enable the Transaction Category "Salary Payment".
- Employees included must have `AccountType = "account"` and a 13‑digit `AccountNumber`.
- Company debit account must be 13 digits.
- Fixed fee pricing must exist in `Pricing` (`TrxCatId = 17`, `Unit = 1`, `Price > 0`).
- Recommend to keep `salary >= fixedFee` validation to avoid overdrafting the employee immediately after crediting.
- Fee GL (13 digits) is either `Pricing.GL1` or derived from the company debit account: `{BRANCH}{932702}{CCY3}`.

## Posting Steps
1) Employee Transfers (gross amounts)
   - For each eligible employee:
     - Amount to credit = `gross`.
     - Build a line to debit the company account and credit the employee account by `gross`.
   - Send a bulk transfer: `POST /api/mobile/PostGroupTransfer`.
   - Parse response; only successful employees move to the fee step.

2) Per‑Employee Fee Transfers (one per successful employee)
   - For each successful employee:
     - Build a transfer to debit the employee account by `fixedFee` and credit the fee GL account by the same amount.
     - Submit either:
       - one request per employee (safest if the bank requires `customerNumber` to match the debit account owner), or
       - a group request if the bank permits multiple different `YBCD06DACC` values in one payload and authorizes debits for each.
   - Narration can use `Pricing.NR2` or default to "Salary receiver‑paid fixed fee".

3) Persist Results
   - Save bank references and raw responses for the gross batch and each fee transfer (or a grouped fee batch if supported).
   - Mark successful salary entries and store timestamps/user.
   - Cycle `TotalAmount` remains the sum of gross salaries for successful employees.

## Accounting Entries
- Batch 1 (gross salaries):
  - Dr Company Debit Account = sum of gross salaries
  - Cr Employee A = gross A
  - Cr Employee B = gross B

- Batch 2 (per‑employee fee):
  - For Employee A: Dr Employee A = fixedFee, Cr Fee GL = fixedFee
  - For Employee B: Dr Employee B = fixedFee, Cr Fee GL = fixedFee

- Company perspective:
  - Total debit equals the gross total (unchanged versus today).
  - Employees see a full credit followed by a fee debit, matching the requested statement presentation.

## Worked Example
Assume:
- Company debit: `1234098765001` (branch `1234`, ccy `001`/LYD)
- Employees: A=`5678123456701` gross `1,000.000`, B=`5678123456702` gross `1,500.000`
- Fixed fee: `2.000` per employee
- Fee GL (derived): `1234932702001`

Entries
- Batch 1 (gross):
  - Dr Company 2,500.000 / Cr A 1,000.000 / Cr B 1,500.000
- Batch 2 (fees):
  - A: Dr A 2.000 / Cr Fee GL 2.000
  - B: Dr B 2.000 / Cr Fee GL 2.000

Totals
- Company total debit: `2,500.000`
- Employees net effect: A `+1,000.000 − 2.000 = 998.000`; B `+1,500.000 − 2.000 = 1,498.000`
- Fee GL credit: `4.000`

## Current Implementation vs Desired
- Current (in code):
  - Pays employees net (gross − fee) from the company account in one batch, then debits the company again once for the aggregated fee to the fee GL.
  - Employees only see the net incoming transfer.
- Desired (this document):
  - Pay employees gross in batch 1.
  - Then for each employee, debit their account by the fixed fee and credit the fee GL (separate per‑employee transactions), so their statement shows both the full credit and the fee deduction.

## Implementation Notes (required changes)
- In `EmployeeSalaryRepository.PostSalaryCycleAsync`:
  - Change employee transfer amount from net to gross.
  - Remove the single aggregated company→GL fee transfer.
  - After parsing employee successes, iterate successful entries and submit per‑employee fee transfer(s) with:
    - `YBCD06DACC` = employee account (13 digits)
    - `YBCD06CACC` = fee GL (13 digits)
    - `YBCD06AMT/AMTC` = fee (scaled/padded as today)
    - Narration from `Pricing.NR2` or default
  - Persist references/responses for each fee debit (or a grouped fee payload if the bank allows mixed DACCs).
  - Keep validations (including `salary >= fixedFee`).

## Bank/API Considerations (confirm with bank)
- Authorization: The current bank client (`customerNumber` derived from the company debit) may not be allowed to debit third‑party customer accounts (employees). If the bank requires the debit party to be the authenticated customer, we must:
  - either obtain bank permission for employer‑initiated per‑employee debits, or
  - have a special internal transfer code for fee offsetting, or
  - fall back to the current model (net pay + company‑funded aggregated fee).
- Grouping: Some cores permit different `YBCD06DACC` per line in one group request; others require one debit owner per request. We can support both with a feature flag.

## Fee GL Determination
- Preferred: `Pricing.GL1` (13 digits).
- Fallback derived from company debit: `{BRANCH}932702{CCY3}` (e.g., `1234098765001` → `1234932702001`).

## References
- Repository: `CompGateApi.Core/Repositories/EmployeeSalaryRepository.cs`
- Models: `CompGateApi.Data/Models/SalaryCycle.cs`, `CompGateApi.Data/Models/SalaryEntry.cs`, `CompGateApi.Data/Models/Employees.cs`, `CompGateApi.Data/Models/Pricing.cs`
- DTOs: `CompGateApi.Core/Dtos/EmployeeSalaryDto.cs`

