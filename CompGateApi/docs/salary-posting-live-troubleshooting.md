Salary Posting — Live Troubleshooting Note (Dec 30)

Context
- You created a salary cycle and clicked Post on live.
- DB row shows: `BankReference=202512301346BA00`, `BankResponseRaw=NULL`, `BankFeeReference=NULL`, `BankFeeResponseRaw=NULL`.
- `TotalAmount` appears unchanged from creation; `BankBatchHistoryJson=NULL`.
- Audit shows the request hit `POST /api/employees/salarycycles/{id}/post` and returned only a trace payload (no cycle DTO).

What This Means
- `BankReference` is set before calling the bank (we save the HID immediately). Seeing it populated means the flow reached the build phase for the employee batch.
- `BankResponseRaw=NULL` means we did not receive/record a response from the bank call. This happens if the code throws before or during the HTTP call (validation failure or transport error) — the raw response is assigned only after a successful HTTP call.
- `BankFeeReference=NULL` is expected if no employee transfers succeeded; the fee batch is posted only after at least one employee credit succeeds.
- `BankBatchHistoryJson=NULL` is expected for an initial post (history accumulates only on repost attempts).

Where It Likely Stopped
Flow (EmployeeSalaryRepository.PostSalaryCycleAsync):
- Package rule check — Salary Payment enabled for the company.
- Eligible entries filter — only `AccountType=account` and 13‑digit `AccountNumber` are included. If none, it throws before setting a bank reference. Your row has a `BankReference`, so it passed this step.
- Fixed fee pricing lookup (TrxCatId=17, Unit=1) and validation — throws if misconfigured. Your row has a `BankReference`, so this was after the HID was set, but a fee/pricing error would still stop the call before `BankResponseRaw` is set.
- Per‑entry validation `amount < fixedFee` — throws before the bank call if any selected employee’s salary is less than the configured fixed fee. This is the most common reason to see `BankReference` set but `BankResponseRaw=NULL`.
- HTTP call to bank — network/transport failure before completing the request will also leave `BankResponseRaw=NULL`.

Likely Causes Checklist
- One or more reposted or initial entries have salary less than the configured fixed fee.
- Fixed fee pricing (TrxCatId=17, Unit=1) is missing or has non‑positive value.
- Debit account not 13 digits or invalid currency code (validated prior to the call).
- Bank API transport issue (connectivity, TLS, DNS) — rarer, but would also result in no `BankResponseRaw`.

How To Verify Quickly
- Pull the configured fixed fee: check `Pricings` where `TrxCatId=17` and `Unit=1`; confirm `Price > 0`, `GL1` is 13 digits (or fee GL derivation applies).
- Inspect cycle entries included in posting: ensure all have `AccountType=account`, 13‑digit `AccountNumber`, and `Amount >= fixedFee`.
- Check application logs for a `PayrollException` message around the trace ID; typical messages:
  - "Salary X is less than the fixed fee Y (employee #ID)."
  - "Pricing for salary fixed fee (TrxCatId=17, Unit=1) is not configured."
  - "Debit account must be 13 digits."
- If the exception is not a PayrollException, look for HTTP client errors when calling `api/mobile/PostBatchApply`.

Expected DB State On Failure
- `BankReference` set (HID like `YYYYMMDDHHmmBA00`).
- `BankResponseRaw` remains NULL.
- `PostedAt`, `PostedByUserId` remain NULL.
- `TotalAmount` unchanged from cycle creation (it’s only recomputed after parsing bank success lines).
- No per‑entry `IsTransferred` flags set.

How To Unblock
- Fix validation issues (raise salaries to be ≥ fixed fee, correct account numbers) and post again.
- If the issue is pricing, configure `Pricings` (TrxCatId=17, Unit=1) with valid `Price` and a 13‑digit `GL1` (or rely on derived GL from the sender account).
- If network-related, verify the Bank API client base address/credentials and connectivity.

Notes
- On success, `BankResponseRaw` is populated, successful entries are marked transferred, `TotalAmount` becomes the gross sum of successful entries, and a fee batch is posted for those successes only.
- Subsequent reposts for failed entries now add newly successful amounts to `TotalAmount` (accumulative, no double counting).

