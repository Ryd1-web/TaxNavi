# Paystack payments

TaxNavi takes payments through Paystack's hosted checkout. **Test mode is the default.** Live keys are refused unless `Paystack:AllowLiveKeys=true`.

## How it works

1. `POST /api/payments/checkout`. The server picks the price (by report type), the email (from the account, or the guest's input) and a unique reference `TNV-yyMMdd-<guid>`. It stores a `Pending` payment, then calls Paystack `transaction/initialize` with the amount in kobo, `callback_url`, and `metadata.cancel_action`.
2. The customer pays on Paystack. Paystack sends them back to `/checkout/callback?reference=…` (or `…&cancelled=1` if they cancel).
3. A report unlocks only after the server calls `transaction/verify/:reference` and gets **status `success`, the same reference, the exact kobo amount and `NGN`**. This happens both from the callback page (`POST /api/payments/verify`) and from the `charge.success` webhook.
4. Fulfilment runs **once**: an atomic `UPDATE … WHERE FulfilledAt IS NULL` decides the single winner when the webhook and the callback race.

Guests do not need an account. A guest report is stored with a SHA-256 hash of a random access token. The browser keeps the token and sends it as `X-Report-Access-Token`. Signing in with the same email moves the report into the account (`POST /api/reports/{id}/claim`).

## Webhook storage and retries

Each signed webhook is **stored first** in `PaymentWebhookEvents`, with the raw payload and status `Received`. It is then processed and marked `Processed` only after every business change has committed. If processing fails, the event is marked `Failed` with `LastError`, and the endpoint returns 500 so Paystack retries. Separately, the `WebhookRetryWorker` re-processes failed or stalled events from the stored payload. Duplicate deliveries are recognised by the unique `EventKey`.

Settings (defaults in brackets): `Webhooks:RetryEnabled` (true), `Webhooks:RetryIntervalSeconds` (60), `Webhooks:RetryAfterSeconds` (120), `Webhooks:MaxAttempts` (8), `Webhooks:RetryBatchSize` (25).

**Retention:** stored payloads include the customer's email address. Choose a retention period (for example, delete processed events after 12 months) to suit your privacy policy and NDPA obligations.

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `Paystack:SecretKey` | User Secrets (dev) / env `Paystack__SecretKey` or `PAYSTACK_SECRET_KEY` | `sk_test_…`. Never put it in `appsettings.json`. |
| `Paystack:AllowLiveKeys` | config / env | `false` by default. Must be `true` for `sk_live_…`. |
| `Paystack:SmePlanCode` | config / env | `PLN_…` turns on recurring SME billing. If empty, the SME plan is a one-off monthly pass. |
| `App:FrontendUrl` | config / env | Base for `callback_url` and `cancel_action`. |
| `TaxRules:Monetization:ReportPrices` | appsettings | Individual ₦3,500 · SelfEmployed ₦3,500 · SME ₦7,000 |
| `TaxRules:Monetization:SmeSubscriptionPrice` | appsettings | ₦7,500. Must equal the Paystack plan amount. |
| `TaxRules:Monetization:SubscriptionGraceDays` | appsettings | Access kept after a failed renewal (default 3). |
| `TaxRules:Monetization:PendingCheckoutMinutes` | appsettings | How long an unpaid checkout is reused (default 30). |
| `RateLimits:*PerMinute` | config / env | Checkout 30, calculate 60, auth 10 (per IP). |

Local development: `dotnet user-secrets set "Paystack:SecretKey" "sk_test_…" --project backend/src/Api`

## Paystack dashboard (Settings → API Keys & Webhooks)

1. **Test webhook URL:** `https://<api-host>/api/payments/webhook/paystack`. It must be publicly reachable over HTTPS; for local testing use a tunnel such as ngrok.
2. **Test callback URL:** `https://<frontend-host>/checkout/callback`. Each transaction also sends its own `callback_url`, which takes precedence.
3. **For recurring SME billing:** Products → Plans → create a **Monthly** plan of **₦7,500**, then copy its `PLN_…` code into `Paystack:SmePlanCode`.
4. **Webhook events used:**
   - `charge.success`
   - `subscription.create`, `subscription.enable`, `subscription.not_renew`, `subscription.disable`, `subscription.expiring_cards`
   - `invoice.create`, `invoice.update`, `invoice.payment_failed`

## API

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/payments/config` | public | `{ enabled, testMode, currency }` (no keys) |
| POST | `/api/payments/checkout` | optional | Report checkout (guest: `email` + `calculation`, or `taxReportId` + `accessToken`); SME plan (signed-in) |
| POST | `/api/payments/verify` | optional | Re-verify with Paystack and fulfil. 200 paid · 202 pending · 402 `PAYMENT_CANCELLED` / `PAYMENT_FAILED` |
| POST | `/api/payments/webhook/paystack` | signature | HMAC-SHA512 of the raw body; idempotent |
| GET | `/api/payments/history` | user | The user's payments |
| GET | `/api/reports/guest/{id}` · `/pdf` | `X-Report-Access-Token` | Guest report and PDF |
| POST | `/api/reports/{id}/claim` | user + token | Move a guest report into the account |
| GET | `/api/subscriptions/current` | user | Plan status and entitlements |
| POST | `/api/subscriptions/checkout` | user | Start the SME plan |
| POST | `/api/subscriptions/cancel` | user | Disable at Paystack; access continues to period end |
| GET | `/api/subscriptions/manage-link` | user | Paystack page for updating the card |

## Testing

- `dotnet test backend/TaxNavi.sln` runs the unit tests (billing, subscriptions, Paystack client, kobo, signatures) and the integration tests. The integration tests boot the real API with a fake Paystack and send signed webhooks.
- **Manual check in test mode:**
  1. Run a calculation, unlock it with any email, and pay with a Paystack test card. Paystack lists its test cards in its docs.
  2. Confirm the report unlocks and the PDF downloads.
  3. Run it again and press Cancel on the checkout page; you should see the cancelled state.
  4. Use the declined-card number to see the failed state.

## Before going live

- Set `sk_live_…` through the hosting secret store and `Paystack:AllowLiveKeys=true`, then configure the **live** webhook URL in the dashboard.
- Serve the API over HTTPS. Set `App:FrontendUrl` and `Cors:AllowedOrigins` to the production domains.
- Create the live ₦7,500 monthly plan if you use recurring billing.
- Rotate any key that has ever been stored in a file in the repository.
- Optional: restrict the webhook endpoint to Paystack's published IP addresses at the proxy or firewall.
