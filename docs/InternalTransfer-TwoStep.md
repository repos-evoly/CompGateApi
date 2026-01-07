Internal Transfer: Two-Step Flow and Foreign Min Commission

Overview

- Two-step internal transfer flow is introduced:
  1) Create draft: persists the transfer in DB in Pending status and returns computed totals and limits.
  2) Execute posting: posts an existing pending transfer to the core bank by id, and marks it Completed on success.
- Service package details now support foreign-currency minimum commission fields so LYD and foreign currencies can have different minimums.

Endpoints

- Create draft
  - POST `/api/transfers/`
  - Body: `TransferRequestCreateDto`
    - `TransactionCategoryId` (int) — usually the InternalTransfer category id
    - `FromAccount` (string)
    - `ToAccount` (string)
    - `Amount` (decimal)
    - `CurrencyDesc` (string, 3-letter code e.g., LYD, USD, EUR)
    - `EconomicSectorId` (int)
    - `Description` (string, optional)
  - Behavior:
    - Validates tenant limits and package entitlements.
    - Computes commission considering percentage and fixed minimum.
    - Uses foreign-currency min commission when currency is not LYD.
    - Persists a `TransferRequest` with `Status = "Pending"`.
  - Response (200):
    - `message: "Transfer created and saved (pending)."`
    - `transfer: TransferRequestDto`
    - `totalTakenFromSender` (string)
    - `totalReceivedByRecipient` (string)
    - `commission` (string)
    - `limits: { globalLimit, dailyLimit, usedToday, monthlyLimit, usedThisMonth }`

- Execute posting
  - POST `/api/transfers/{id:int}/post`
  - Behavior:
    - Verifies the transfer belongs to the caller’s company and is Pending.
    - Posts to core bank using saved amounts and commission.
    - Updates `Status = "Completed"` and sets `BankReference` on success.
  - Response (200):
    - `message: "Transfer posted successfully"`
    - `transfer: TransferRequestDto`
    - `totalTakenFromSender` (string)
    - `totalReceivedByRecipient` (string)

Commission Calculation

- Commission is the greater of:
  - Percentage: `Amount * (B2B/B2C CommissionPct / 100)`
  - Minimum (fixed fee):
    - For LYD: `B2BFixedFee` or `B2CFixedFee`
    - For foreign (non-LYD): `B2BFixedFeeForeign` or `B2CFixedFeeForeign` if set; otherwise falls back to the LYD min
- Rounding: LYD amounts are rounded to 3 decimals, USD/EUR to 2 decimals.
- Sender/receiver totals respect `Company.CommissionOnReceiver`.

Service Package Changes

- Data model: `ServicePackageDetail` adds two nullable fields:
  - `B2BFixedFeeForeign` (decimal?)
  - `B2CFixedFeeForeign` (decimal?)
- API DTOs now include these new fields for read/update:
  - ServicePackageCategoryDto: `B2BFixedFeeForeign`, `B2CFixedFeeForeign`
  - ServicePackageCategoryUpdateDto: `B2BFixedFeeForeign`, `B2CFixedFeeForeign`
- Endpoints `/api/servicepackages` map these fields for get and update operations.

Data Migration

- The two new columns must be added to the database table `ServicePackageDetails`.
- Generate and apply a migration (example):
  - Add columns `B2BFixedFeeForeign` and `B2CFixedFeeForeign` as `decimal(18,2)` or your standard precision.
  - No default is required; when null, logic falls back to the LYD min fields.

Usage Examples

1) Create a draft transfer

POST /api/transfers
{
  "transactionCategoryId": 2,
  "fromAccount": "0015798000123",
  "toAccount": "0015798000456",
  "amount": 1000.00,
  "currencyDesc": "USD",
  "economicSectorId": 5,
  "description": "Invoice 123"
}

Response
{
  "message": "Transfer created and saved (pending).",
  "transfer": { ... },
  "totalTakenFromSender": "1000.00",
  "totalReceivedByRecipient": "995.00",
  "commission": "5.00",
  "limits": { ... }
}

2) Post a pending transfer

POST /api/transfers/123/post

Response
{
  "message": "Transfer posted successfully",
  "transfer": { ... },
  "totalTakenFromSender": "1000.00",
  "totalReceivedByRecipient": "995.00"
}

Notes

- If a package has the LYD min commission set to 10 and `B2BFixedFeeForeign/B2CFixedFeeForeign` set to 2, USD transfers will use 2 as the minimum, avoiding disproportionately high minimums for foreign currencies.
- If the foreign fields are left null, the system falls back to the LYD min values.
