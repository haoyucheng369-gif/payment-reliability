import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

const baseUrl = __ENV.BASE_URL ?? 'http://host.docker.internal:5147'

export const options = {
  vus: Number(__ENV.VUS ?? 20),
  iterations: Number(__ENV.ITERATIONS ?? 100),
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
}

function createOrderWithRetry(correlationId) {
  for (let attempt = 1; attempt <= 10; attempt += 1) {
    const response = http.post(
      `${baseUrl}/orders`,
      JSON.stringify({
        amount: 42.5,
        currency: 'EUR',
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': correlationId,
        },
      },
    )

    if (response.status === 200) {
      return response
    }

    // Docker 刚启动时 API 可能还没准备好，短暂等待后重试。
    sleep(1)
  }

  return null
}

export default function () {
  group('api order and payment creation baseline', () => {
    const correlationId = `CORR-${crypto.randomUUID()}`
    const orderResponse = createOrderWithRetry(correlationId)

    check(orderResponse, {
      'order created': (response) => response !== null && response.status === 200,
      'order has id': (response) => response !== null && Boolean(response.json('id')),
    })

    if (orderResponse === null || orderResponse.status !== 200) {
      console.error('Order creation failed; skipping payment request.')
      return
    }

    const order = orderResponse.json()

    const paymentResponse = http.post(
      `${baseUrl}/payments`,
      JSON.stringify({
        orderId: order.id,
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': correlationId,
        },
      },
    )

    check(paymentResponse, {
      'payment created or idempotent hit': (response) => response.status === 200,
      'payment has id': (response) => response.status === 200 && Boolean(response.json('id')),
      'payment belongs to order': (response) => response.status === 200 && response.json('orderId') === order.id,
    })

    if (paymentResponse.status !== 200) {
      console.error(`Payment request failed with status ${paymentResponse.status}.`)
      return
    }

    const payment = paymentResponse.json()
    const paymentLookupResponse = http.get(`${baseUrl}/payments/${payment.id}`)

    check(paymentLookupResponse, {
      'payment lookup succeeded': (response) => response.status === 200,
      'payment lookup returns same payment': (response) => response.status === 200 && response.json('id') === payment.id,
    })
  })
}
