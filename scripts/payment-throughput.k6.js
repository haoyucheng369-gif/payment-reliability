import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

const baseUrl = __ENV.BASE_URL ?? 'http://host.docker.internal:5147'
const finalStatusTimeoutSeconds = Number(__ENV.FINAL_STATUS_TIMEOUT_SECONDS ?? 20)

export const options = {
  vus: Number(__ENV.VUS ?? 10),
  iterations: Number(__ENV.ITERATIONS ?? 20),
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
}

export default function () {
  group('different orders payment throughput', () => {
    const orderResponse = http.post(
      `${baseUrl}/orders`,
      JSON.stringify({
        amount: 88.99,
        currency: 'EUR',
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': `CORR-${crypto.randomUUID()}`,
        },
      },
    )

    check(orderResponse, {
      'order created': (response) => response.status === 200,
      'order has id': (response) => Boolean(response.json('id')),
    })

    const order = orderResponse.json()

    const paymentResponse = http.post(
      `${baseUrl}/payments`,
      JSON.stringify({
        orderId: order.id,
      }),
      {
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': `CORR-${crypto.randomUUID()}`,
        },
      },
    )

    check(paymentResponse, {
      'payment created': (response) => response.status === 200,
      'payment has id': (response) => Boolean(response.json('id')),
      'payment belongs to created order': (response) => response.json('orderId') === order.id,
    })

    const payment = paymentResponse.json()
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

    const getOrderResponse = http.get(`${baseUrl}/orders/${order.id}`)
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
  })
}
