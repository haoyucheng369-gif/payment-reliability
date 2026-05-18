import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

const finalStatusTimeoutSeconds = Number(__ENV.FINAL_STATUS_TIMEOUT_SECONDS ?? 15)

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

export function teardown(data) {
  group('final async payment result', () => {
    const idempotentPaymentResponse = http.post(
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

    check(idempotentPaymentResponse, {
      'idempotent payment lookup succeeded': (response) => response.status === 200,
      'idempotent payment belongs to same order': (response) => response.json('orderId') === data.order.id,
    })

    const payment = idempotentPaymentResponse.json()
    let finalPayment = payment

    for (let elapsedSeconds = 0; elapsedSeconds < finalStatusTimeoutSeconds; elapsedSeconds += 1) {
      const getPaymentResponse = http.get(`${baseUrl}/payments/${payment.id}`)

      check(getPaymentResponse, {
        'payment status lookup succeeded': (response) => response.status === 200,
      })

      finalPayment = getPaymentResponse.json()

      if (finalPayment.status === 'Succeeded') {
        break
      }

      sleep(1)
    }

    const getOrderResponse = http.get(`${baseUrl}/orders/${data.order.id}`)
    const finalOrder = getOrderResponse.json()

    check(finalPayment, {
      'final payment status is Succeeded': (paymentResult) => paymentResult.status === 'Succeeded',
    })

    check(getOrderResponse, {
      'order status lookup succeeded': (response) => response.status === 200,
    })

    check(finalOrder, {
      'final order status is Paid': (orderResult) => orderResult.status === 'Paid',
    })

    console.log(`PaymentId: ${payment.id}`)
    console.log(`FinalPaymentStatus: ${finalPayment.status}`)
    console.log(`FinalOrderStatus: ${finalOrder.status}`)
  })
}
