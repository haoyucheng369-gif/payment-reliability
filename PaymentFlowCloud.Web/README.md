# PaymentFlowCloud Web

Small React checkout simulator for local payment-flow testing.

## Purpose

This UI is intentionally narrow:

- submit a fixed checkout order
- reuse the same `MerchantOrderId` to verify idempotency
- create a new order for the next test run
- poll `GET /payments/{id}` until the async worker updates the payment status

It is a local verification tool, not a full ecommerce frontend.

## Run

Start the backend API and worker first:

```powershell
dotnet run --project ../PaymentFlowCloud.Api --launch-profile http
dotnet run --project ../PaymentFlowCloud.Worker
```

Then run the web app:

```powershell
npm install
npm run dev
```

The Vite dev server proxies `/payments` to `http://localhost:5147`.
