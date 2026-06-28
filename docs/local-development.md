# Local Development

This document describes the local Docker Compose environment and the main validation scenarios.

## Local Stack

Docker Compose starts:

| Service | Purpose |
| --- | --- |
| `api` | ASP.NET Core Payment API, Swagger, metrics, webhook endpoint |
| `worker` | RabbitMQ consumer and provider caller |
| `provider-mock` | Fake external payment provider and webhook sender |
| `web` | React checkout simulation UI |
| `sqlserver` | Local SQL Server |
| `rabbitmq` | RabbitMQ broker and management UI |
| `seq` | Structured log viewer |
| `prometheus` | Metrics scraper |
| `grafana` | Metrics and trace UI |
| `tempo` | Distributed trace storage |
| `k6-*` | On-demand load and reliability tests |

## Start Locally

Start everything:

```powershell
docker compose up -d --build
```

Docker Compose normally applies EF Core migrations when the API starts in Development mode. Run migrations manually only when running the API outside Docker or when forcing a schema update:

```powershell
dotnet ef database update --project ReliablePaymentProcessing.Infrastructure --startup-project ReliablePaymentProcessing.Api
```

Useful URLs:

| Tool | URL |
| --- | --- |
| React UI | http://localhost:5173 |
| Swagger | http://localhost:5147/swagger |
| RabbitMQ | http://localhost:15672 |
| Seq | http://localhost:5341 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3000 |
| Tempo | http://localhost:3200 |
| ProviderMock | http://localhost:5290/provider/payments |

Default local credentials:

| Tool | Credentials |
| --- | --- |
| RabbitMQ | `guest / guest` |
| Grafana | `admin / admin` |

## Demo Scenarios

### 1. Normal Checkout Flow

Open the React UI:

```text
http://localhost:5173
```

Expected result:

```text
Order = Paid
Payment = Succeeded
```

### 2. Same Order, Concurrent Payment Requests

This sends concurrent `POST /payments` requests for the same order.

```powershell
docker compose run --rm --no-deps -e VUS=20 -e ITERATIONS=20 -e FINAL_STATUS_TIMEOUT_SECONDS=15 k6
```

Expected result:

```text
Only one payment is created for the order.
Payment eventually becomes Succeeded.
Order eventually becomes Paid.
```

### 3. API Throughput Baseline

This measures the synchronous API boundary:

```text
POST /orders
POST /payments
GET /payments/{id}
```

Run:

```powershell
docker compose run --rm --no-deps -e VUS=20 -e ITERATIONS=100 k6-api-throughput
```

Use this to observe request rate, latency, SQL insert cost, and RabbitMQ publish overhead.

### 4. Full Async Payment Throughput

This creates different orders and payments, then waits for the provider/webhook flow.

```powershell
docker compose run --rm --no-deps -e VUS=10 -e ITERATIONS=20 -e FINAL_STATUS_TIMEOUT_SECONDS=20 k6-throughput
```

Expected result:

```text
Payment = Succeeded
Order = Paid
```

### 5. Duplicate Webhook Idempotency

This posts the same successful provider webhook multiple times.

```powershell
docker compose run --rm --no-deps -e DUPLICATE_WEBHOOK_COUNT=3 -e FINAL_STATUS_TIMEOUT_SECONDS=20 k6-webhook-duplicate
```

Expected result:

```text
Payment remains Succeeded.
Order remains Paid.
```

### 6. Provider Failure and DLQ

HTTP 500 failure:

```powershell
$env:PROVIDER_MOCK_MODE="Http500"
docker compose up -d --build provider-mock worker
docker compose run --rm --no-deps -e DLQ_TIMEOUT_SECONDS=20 k6-provider-failure
```

Timeout failure:

```powershell
$env:PROVIDER_MOCK_MODE="Timeout"
docker compose up -d --build provider-mock worker
docker compose run --rm --no-deps -e DLQ_TIMEOUT_SECONDS=30 k6-provider-failure
```

Restore success mode:

```powershell
$env:PROVIDER_MOCK_MODE="Success"
docker compose up -d --build provider-mock worker
```

Expected failure result:

```text
Payment remains Pending.
Order remains PendingPayment.
payment-created message reaches payment-created-dlq.
```

### 7. Worker Scaling

Run multiple Worker instances:

```powershell
docker compose up -d --build --scale worker=5
```

Current Worker tuning options:

| Option | Meaning |
| --- | --- |
| `RabbitMQ__PrefetchCount` | How many unacknowledged messages RabbitMQ can deliver to one Worker instance |
| `RabbitMQ__MaxConcurrentMessages` | How many messages one Worker process handles at the same time |

With `worker=5` and `MaxConcurrentMessages=5`, the local stack can process up to about 25 messages concurrently.
