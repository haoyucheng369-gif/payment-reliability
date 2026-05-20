# PaymentFlowCloud

PaymentFlowCloud is a local-first payment reliability playground built with ASP.NET Core, SQL Server, RabbitMQ, Docker Compose, Seq, Prometheus, Grafana, React, and k6.

It is not intended to become a real payment provider. The goal is to practice practical backend reliability patterns around payment creation, idempotency, asynchronous processing, webhooks, retries, DLQ handling, load testing, and observability.

## Current Capabilities

- Order creation API
- Idempotent payment creation by `OrderId`
- SQL Server persistence with EF Core migrations
- RabbitMQ `payment-created` queue
- Background Worker consumer
- Fake payment provider with delayed webhook callback
- Payment status flow: `Pending -> Processing -> Succeeded`
- Order status flow: `PendingPayment -> Paid`
- Provider failure simulation: `Success`, `Http500`, `Timeout`
- HMAC-signed fake provider webhooks
- Worker retry and DLQ handling
- Multi-worker local scaling
- Seq structured logs with `CorrelationId`
- Prometheus and Grafana API metrics
- React checkout simulation UI
- k6 scripts for idempotency, throughput, duplicate webhooks, and provider failure

## Architecture

```text
React Web
   |
   v
Payment API
   |
   +--> SQL Server
   |
   +--> RabbitMQ payment-created
             |
             v
        Worker Consumer
             |
             v
        Fake Provider
             |
             v
        API Webhook
             |
             v
        SQL Server status update
```

Failure path:

```text
Worker calls Fake Provider
   |
   +--> Provider returns 500 or times out
   |
   +--> Worker retries payment-created
   |
   +--> Max retries reached
   |
   v
payment-created-dlq
```

## Local Stack

Docker Compose starts:

- `api`: ASP.NET Core Payment API
- `worker`: RabbitMQ consumer and provider caller
- `provider-mock`: fake payment provider and webhook sender
- `web`: React checkout UI
- `sqlserver`: local SQL Server
- `rabbitmq`: RabbitMQ with management UI
- `seq`: local structured log viewer
- `k6-*`: on-demand load test runners

## Run Locally

Start the stack:

```powershell
docker compose up -d --build
```

Apply database migrations:

```powershell
dotnet ef database update --project PaymentFlowCloud.Infrastructure --startup-project PaymentFlowCloud.Api
```

Useful local URLs:

```text
Web UI:      http://localhost:5173
Swagger:     http://localhost:5147/swagger
RabbitMQ:    http://localhost:15672
Seq:         http://localhost:5341
Prometheus:  http://localhost:9090
Grafana:     http://localhost:3000
Provider:    http://localhost:5290/provider/payments
```

RabbitMQ credentials:

```text
guest / guest
```

## Payment Flow

Normal flow:

```text
POST /orders
-> Order = PendingPayment
-> POST /payments
-> Payment = Pending
-> API publishes payment-created
-> Worker consumes payment-created
-> Worker calls Fake Provider
-> Worker marks Payment = Processing
-> Fake Provider calls signed webhook after delay
-> API marks Payment = Succeeded
-> API marks Order = Paid
```

Idempotency rule:

```text
One OrderId can create only one Payment.
```

The database unique index on `Payments.OrderId` is the final concurrency guard.

## Load Tests

Start the local stack first:

```powershell
docker compose up -d --build
```

### Payment Idempotency

Sends concurrent `POST /payments` requests for the same order.

```powershell
docker compose run --rm --no-deps -e VUS=20 -e ITERATIONS=20 -e FINAL_STATUS_TIMEOUT_SECONDS=15 k6
```

Expected result:

```text
Only one payment is created for the order.
Payment eventually becomes Succeeded.
Order eventually becomes Paid.
```

### API Throughput Baseline

Measures the synchronous API boundary only:

```text
POST /orders
POST /payments
GET /payments/{id}
```

Run:

```powershell
docker compose run --rm --no-deps -e VUS=20 -e ITERATIONS=100 k6-api-throughput
```

This is useful for checking API latency, SQL insert cost, and RabbitMQ publish overhead without waiting for provider/webhook completion.

### Full Payment Throughput

Creates different orders and payments, then waits for the async provider/webhook flow to complete.

```powershell
docker compose run --rm --no-deps -e VUS=10 -e ITERATIONS=20 -e FINAL_STATUS_TIMEOUT_SECONDS=20 k6-throughput
```

Expected result:

```text
Payment = Succeeded
Order = Paid
```

### Duplicate Webhook Idempotency

Posts the same successful provider webhook multiple times.

```powershell
docker compose run --rm --no-deps -e DUPLICATE_WEBHOOK_COUNT=3 -e FINAL_STATUS_TIMEOUT_SECONDS=20 k6-webhook-duplicate
```

Expected result:

```text
Payment remains Succeeded.
Order remains Paid.
```

The script signs each duplicate webhook with the local fake provider secret:

```text
X-Provider-Timestamp
X-Provider-Signature
```

### Provider Failure and DLQ

Switch the fake provider mode, rebuild the provider and worker, then run the DLQ verification script.

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

## Worker Scaling

Run multiple Worker instances locally:

```powershell
docker compose up -d --build --scale worker=5
```

The Worker service does not use a fixed `container_name`, so Docker Compose can create multiple replicas.

Current Worker tuning options:

```text
RabbitMQ__PrefetchCount
RabbitMQ__MaxConcurrentMessages
```

`PrefetchCount` controls how many unacknowledged messages RabbitMQ can deliver to one Worker instance.

`MaxConcurrentMessages` controls how many messages one Worker process handles at the same time.

With `worker=5` and `MaxConcurrentMessages=5`, the local stack can process up to about 25 messages concurrently.

## Observability

The API exposes Prometheus metrics:

```text
http://localhost:5147/metrics
```

Prometheus scrapes the API every 5 seconds:

```text
http://localhost:9090
```

Grafana is preconfigured with a Prometheus datasource and a `PaymentFlowCloud API` dashboard:

```text
http://localhost:3000
```

Grafana credentials:

```text
admin / admin
```

The local API dashboard focuses on:

```text
API request rate
API p95 / p99 latency
API 5xx error ratio
API responses by status code
```

Run k6 while Grafana is open to see the API metrics move:

```powershell
docker compose run --rm --no-deps -e VUS=100 -e ITERATIONS=3000 k6-api-throughput
```

The local stack sends structured logs to Seq:

```text
http://localhost:5341
```

The API accepts an optional correlation header:

```http
X-Correlation-Id: CORR-123
```

If missing, the API generates one and returns it in the response header. The same `CorrelationId` is stored on the payment, published in the RabbitMQ message, and used by the Worker log scope.

Useful Seq queries:

```text
CorrelationId = 'CORR-123'
PaymentId = '...'
OrderId = '...'
```

## RabbitMQ

Local queues:

```text
payment-created
payment-created-dlq
```

Retry behavior:

```text
Worker consumes payment-created
-> success: ack
-> failure and x-retry-count < 3: republish to payment-created with x-retry-count + 1, then ack original message
-> failure and x-retry-count >= 3: publish to payment-created-dlq, then ack original message
```

This version intentionally uses immediate fixed-count retry instead of delayed retry queues so the failure flow stays easy to inspect.

## Project Structure

```text
PaymentFlowCloud.Api             HTTP API, controllers, middleware, Swagger
PaymentFlowCloud.Application     Use cases, service interfaces, contracts
PaymentFlowCloud.Domain          Entities, statuses, state transition rules
PaymentFlowCloud.Infrastructure  EF Core, repositories, RabbitMQ, provider client
PaymentFlowCloud.Worker          RabbitMQ consumer and background processing
PaymentFlowCloud.ProviderMock    Fake external payment provider
PaymentFlowCloud.Web             React checkout simulation UI
scripts                          k6 load and reliability tests
```

## Current Reliability Features

- Database unique constraint for payment idempotency
- RabbitMQ queue buffering
- Worker prefetch and local concurrency control
- Multi-worker scaling
- Fixed retry count
- DLQ fallback
- Duplicate webhook safety
- HMAC webhook signature validation
- Provider timeout and HTTP 500 simulation
- Operational indexes on `(Status, CreatedAt)` for order/payment scans

## Roadmap

Near-term priorities:

- Prometheus and Grafana for API metrics
- OpenTelemetry tracing across API, Worker, Provider, and webhook
- Azure Application Insights integration
- Azure migration path with Container Apps and queue-based processing
- Optional operational dashboards for queue backlog and payment states

Deferred intentionally:

- Complex delayed retry topology
- DLQ replay tooling
- Redis distributed locking
- Heavy CQRS/MediatR ceremony
- Production payment provider integration
