# Observability

PaymentFlowCloud uses three complementary observability signals:

- Metrics for system-level trends
- Traces for single payment request chains
- Logs for detailed event context

## Metrics

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

The dashboard focuses on:

- API request rate
- API p95 / p99 latency
- API 5xx error ratio
- API responses by status code

Run k6 while Grafana is open to see metrics move:

```powershell
docker compose run --rm --no-deps -e VUS=100 -e ITERATIONS=3000 k6-api-throughput
```

## Traces

OpenTelemetry sends distributed traces to Tempo:

```text
API / Worker / ProviderMock -> OTLP -> Tempo -> Grafana
```

When `APPLICATIONINSIGHTS_CONNECTION_STRING` is configured, the same trace spans are also exported to Azure Monitor / Application Insights:

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING="<your-application-insights-connection-string>"
docker compose up -d --build api worker provider-mock
```

Tempo is provisioned as a Grafana datasource:

```text
http://localhost:3000
```

Use Grafana `Explore`, select the `Tempo` datasource, then search by trace attributes such as:

```text
correlation.id = "CORR-123"
payment.id = "..."
order.id = "..."
```

The main payment trace shows:

```text
POST /payments
-> rabbitmq publish payment-created
-> rabbitmq consume payment-created
-> Worker HTTP call to ProviderMock
-> Provider webhook delivery delay
-> API webhook callback
-> payment/order completion
```

## Logs

The local stack sends structured logs to Seq:

```text
http://localhost:5341
```

The API accepts an optional correlation header:

```http
X-Correlation-Id: CORR-123
```

If missing, the API generates one and returns it in the response header. The same `CorrelationId` is stored on the payment, published in the RabbitMQ message, and used by the Worker and Provider logs.

Useful Seq queries:

```text
CorrelationId = 'CORR-123'
PaymentId = '...'
OrderId = '...'
```

## Signal Roles

| Signal | Primary Question |
| --- | --- |
| Metrics | Is the system healthy overall? |
| Traces | What happened in this single payment flow? |
| Logs | What exact business and technical events happened? |
| CorrelationId | Which logs/traces belong to the same business request? |

## Local and Azure Mapping

| Local | Azure |
| --- | --- |
| Seq structured logs | Application Insights logs / Log Analytics |
| Prometheus metrics | Application Insights metrics / Azure Monitor metrics |
| Grafana dashboard | Azure Monitor Workbooks / Application Insights dashboards |
| Tempo traces | Application Insights distributed tracing |
| `CorrelationId` log scope | Application Insights custom property |
| OpenTelemetry trace spans | Application Insights dependencies and request traces |
