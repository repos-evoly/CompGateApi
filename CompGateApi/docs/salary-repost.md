Salary Repost

Overview
- Per-entry results are now persisted and returned when posting a salary cycle. Each entry records transfer status, code, and reason from the core (bank) response.
- Failed entries can be edited and reposted without affecting successful ones.
- Each repost attempt appends a record into a cycle-level JSON history with the salary batch reference and the commission (fee) batch reference.

Data Changes
- SalaryEntry
  - isTransferred: bool
  - transferredAt: datetime
  - transferResultCode: string? (e.g., "S" for success, other codes for failure)
  - transferResultReason: string? (bank/core reason if provided)
  - bankLineResponseRaw: string? (raw JSON for the specific line)
  - No per-entry override fields; edits are applied to Employee directly.
- SalaryCycle
  - bankReference: string? (last salary batch reference)
  - bankResponseRaw: string? (last salary batch raw response)
  - bankFeeReference: string? (last commission batch reference)
  - bankFeeResponseRaw: string? (last commission batch raw response)
  - bankBatchHistoryJson: string? JSON array; each element is
    - attemptAtUtc: string (ISO-8601)
    - entryIds: number[] (entry IDs included in that attempt)
    - salaryRef: string (PostBatchApply HID for employee credits)
    - feeRef: string|null (PostBatchApply HID for fee settlement if any entries succeeded)

Posting Flow
1) Create a salary cycle with entries.
2) POST /api/employees/salarycycles/{id}/post
   - Sends a single batch apply for all eligible entries (13-digit accounts).
   - Parses per-line results:
     - Success lines set entry.isTransferred, transferredAt and clear reason.
     - Failure lines set transferResultCode, transferResultReason, and bankLineResponseRaw.
   - Posts a second batch apply for commission (fee) for succeeded entries.
   - Returns the cycle DTO including entries with their result fields.

Reposting Failed Entries (and editing first)
- Get failed entries for a cycle: GET /api/employees/salarycycles/{id}/entries/failed
- Edit a specific failed entry and its employee account: POST /api/employees/salarycycles/{cycleId}/entries/{entryId}/edit
  - Body:
    { "amount": 450.00, "accountNumber": "0012123456789", "accountType": "account", "evoWallet": null, "bcdWallet": null }
  - Any subset of these fields may be provided; only non-null fields are updated.
- Repost only selected failed entries by IDs: POST /api/employees/salarycycles/{id}/repost
  - Body: { "entryIds": [123, 124, 200] }
  - Only entries with isTransferred = false are considered; repost uses the current Employee account fields.
  - Per-line responses update transferResultCode, transferResultReason, and bankLineResponseRaw.
  - If any entries succeed, a commission (fee) batch apply is posted for those successes only.
  - Cycle totalAmount accumulates: amounts of newly successful entries are added to the existing total (no double counting).
  - The pair { salaryRef, feeRef } for this attempt is appended to salaryCycle.bankBatchHistoryJson along with the set of entryIds.
  - Response: the updated salary cycle DTO (same shape as GET) including updated entries.

DTOs
- SalaryRepostRequestDto
  - items: SalaryRepostItemDto[]
- SalaryRepostItemDto
  - entryId: number (required)
  - newAmount: number? (optional)
  - newAccountNumber: string? (optional)
  - newAccountType: string? (optional)

Returned Fields
- SalaryEntryDto now includes:
  - isTransferred, transferredAt
  - transferResultCode, transferResultReason
- SalaryCycleDto now includes:
  - bankReference, bankResponseRaw
  - bankBatchHistoryJson (string JSON)
- Admin list/detail also expose bankBatchHistoryJson and fee references.

Example Sequence
1) Create cycle, then post: POST /api/employees/salarycycles/{id}/post
   - Response shows which entries succeeded and which failed with reasons.
2) Fix failed entries and repost only those: POST /api/employees/salarycycles/{id}/repost
   - Include edits per entry in the items array.
3) Review cycle history JSON on the cycle to see each attempt’s salaryRef and feeRef as you iterate fixes.

Notes
- The service reads per-line fields from the bank response:
  - YBCD10RESP => code ("S" = success)
  - YBCD10ACC => account posted for the line
  - YBCD10REAS/REASON/MESSAGE (best-effort) => reason message if available
- Reposts always use the current employee account fields (after any edits you make with the edit endpoint).
