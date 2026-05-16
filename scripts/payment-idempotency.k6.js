import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

export const options = {
  vus: Number(__ENV.VUS ?? 20),
  iterations: Number(__ENV.ITERATIONS ?? 20),
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
}

const baseUrl = __ENV.BASE_URL ?? 'http://host.docker.internal:5147'

export function setup() {
  let createOrderResponse

  for (let attempt = 1; attempt <= 10; attempt += 1) {
    createOrderResponse = http.post(
      `${baseUrl}/orders`,
      JSON.stringify({
        amount: 66.77,
        currency: 'EUR',
      }),
      {
        headers: {
          'Content-Type': 'application/json',
        },
      },
    )

    if (createOrderResponse.status === 200) {
      break
    }

    console.log(`Waiting for API readiness, attempt ${attempt}/10`)
    sleep(1)
  }

  check(createOrderResponse, {
    'order created': (response) => response.status === 200,
  })

  const order = createOrderResponse.json()
  console.log(`OrderId: ${order.id}`)
  console.log(`MerchantOrderId: ${order.merchantOrderId}`)

  return {
    order,
  }
}

export default function (data) {
  group('same order payment idempotency', () => {
    const createPaymentResponse = http.post(
      `${baseUrl}/payments`,
      JSON.stringify({
        orderId: data.order.id,
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': `CORR-${crypto.randomUUID()}`,
        },
      },
    )

    check(createPaymentResponse, {
      'payment request succeeded': (response) => response.status === 200,
      'payment has id': (response) => Boolean(response.json('id')),
      'payment belongs to same order': (response) => response.json('orderId') === data.order.id,
    })
  })
}
