import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

const baseUrl = __ENV.BASE_URL ?? 'http://host.docker.internal:5147'
const duplicateWebhookCount = Number(__ENV.DUPLICATE_WEBHOOK_COUNT ?? 3)
const finalStatusTimeoutSeconds = Number(__ENV.FINAL_STATUS_TIMEOUT_SECONDS ?? 20)

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
}

export function setup() {
  const correlationId = `CORR-${crypto.randomUUID()}`

  const orderResponse = http.post(
    `${baseUrl}/orders`,
    JSON.stringify({
      amount: 77.88,
      currency: 'EUR',
    }),
    {
      headers: {
        'Content-Type': 'application/json',
        'X-Correlation-Id': correlationId,
      },
    },
  )

  check(orderResponse, {
    'order created': (response) => response.status === 200,
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
        'X-Correlation-Id': correlationId,
      },
    },
  )

  check(paymentResponse, {
    'payment created': (response) => response.status === 200,
    'payment belongs to order': (response) => response.json('orderId') === order.id,
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

  check(finalPayment, {
    'payment reached Succeeded before duplicate webhook test': (paymentResult) => paymentResult.status === 'Succeeded',
  })

  console.log(`OrderId: ${order.id}`)
  console.log(`PaymentId: ${payment.id}`)

  return {
    order,
    payment,
    correlationId,
  }
}

export default function (data) {
  group('duplicate succeeded webhook is idempotent', () => {
    for (let index = 1; index <= duplicateWebhookCount; index += 1) {
      const duplicateWebhookResponse = http.post(
        `${baseUrl}/webhooks/fake-provider/payment-succeeded`,
        JSON.stringify({
          paymentId: data.payment.id,
          providerPaymentId: `fp_duplicate_${data.payment.id}`,
          status: 'Succeeded',
          correlationId: data.correlationId,
        }),
        {
          headers: {
            'Content-Type': 'application/json',
            'X-Correlation-Id': data.correlationId,
          },
        },
      )

      check(duplicateWebhookResponse, {
        [`duplicate webhook ${index} accepted`]: (response) => response.status === 200,
      })
    }
  })
}

export function teardown(data) {
  const paymentResponse = http.get(`${baseUrl}/payments/${data.payment.id}`)
  const orderResponse = http.get(`${baseUrl}/orders/${data.order.id}`)

  check(paymentResponse, {
    'payment lookup after duplicate webhooks succeeded': (response) => response.status === 200,
  })

  check(orderResponse, {
    'order lookup after duplicate webhooks succeeded': (response) => response.status === 200,
  })

  const payment = paymentResponse.json()
  const order = orderResponse.json()

  check(payment, {
    'payment remains Succeeded after duplicate webhooks': (paymentResult) => paymentResult.status === 'Succeeded',
  })

  check(order, {
    'order remains Paid after duplicate webhooks': (orderResult) => orderResult.status === 'Paid',
  })

  console.log(`FinalPaymentStatus: ${payment.status}`)
  console.log(`FinalOrderStatus: ${order.status}`)
}
