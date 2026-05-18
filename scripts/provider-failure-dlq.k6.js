import encoding from 'k6/encoding'
import http from 'k6/http'
import { check, group } from 'k6'
import { sleep } from 'k6'

const baseUrl = __ENV.BASE_URL ?? 'http://host.docker.internal:5147'
const rabbitMqManagementUrl = __ENV.RABBITMQ_MANAGEMENT_URL ?? 'http://host.docker.internal:15672'
const rabbitMqVhost = encodeURIComponent(__ENV.RABBITMQ_QUEUE_VHOST ?? '/')
const rabbitMqDlqName = encodeURIComponent(__ENV.RABBITMQ_DLQ_NAME ?? 'payment-created-dlq')
const rabbitMqUser = __ENV.RABBITMQ_USER ?? 'guest'
const rabbitMqPassword = __ENV.RABBITMQ_PASSWORD ?? 'guest'
const dlqTimeoutSeconds = Number(__ENV.DLQ_TIMEOUT_SECONDS ?? 20)

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
}

function rabbitMqHeaders() {
  return {
    Authorization: `Basic ${encoding.b64encode(`${rabbitMqUser}:${rabbitMqPassword}`)}`,
  }
}

function getDeadLetterQueueMessageCount() {
  const response = http.get(
    `${rabbitMqManagementUrl}/api/queues/${rabbitMqVhost}/${rabbitMqDlqName}`,
    {
      headers: rabbitMqHeaders(),
    },
  )

  check(response, {
    'RabbitMQ DLQ lookup succeeded': (result) => result.status === 200,
  })

  if (response.status !== 200) {
    return 0
  }

  return Number(response.json('messages') ?? 0)
}

function createOrderWithRetry(correlationId) {
  for (let attempt = 1; attempt <= 15; attempt += 1) {
    const response = http.post(
      `${baseUrl}/orders`,
      JSON.stringify({
        amount: 99.01,
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

    // Docker 刚启动时 API 可能还没监听端口，脚本短暂等待后重试。
    sleep(1)
  }

  return null
}

export function setup() {
  return {
    initialDlqMessages: getDeadLetterQueueMessageCount(),
  }
}

export default function (data) {
  group('provider failure moves payment-created message to DLQ', () => {
    const correlationId = `CORR-${crypto.randomUUID()}`

    const orderResponse = createOrderWithRetry(correlationId)

    check(orderResponse, {
      'order created': (response) => response !== null && response.status === 200,
    })

    if (orderResponse === null || orderResponse.status !== 200) {
      console.error('Order creation failed; API may not be ready.')
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
      'payment request accepted by API': (response) => response.status === 200,
      'payment belongs to order': (response) => response.status === 200 && response.json('orderId') === order.id,
    })

    if (paymentResponse.status !== 200) {
      console.error(`Payment creation failed with status ${paymentResponse.status}.`)
      return
    }

    const payment = paymentResponse.json()
    let finalDlqMessages = data.initialDlqMessages

    for (let elapsedSeconds = 0; elapsedSeconds < dlqTimeoutSeconds; elapsedSeconds += 1) {
      finalDlqMessages = getDeadLetterQueueMessageCount()

      if (finalDlqMessages > data.initialDlqMessages) {
        break
      }

      sleep(1)
    }

    const finalPaymentResponse = http.get(`${baseUrl}/payments/${payment.id}`)
    const finalPayment = finalPaymentResponse.json()
    const finalOrderResponse = http.get(`${baseUrl}/orders/${order.id}`)
    const finalOrder = finalOrderResponse.json()

    check({ finalDlqMessages, initialDlqMessages: data.initialDlqMessages }, {
      'DLQ received failed payment-created message': (result) => result.finalDlqMessages > result.initialDlqMessages,
    })

    check(finalPaymentResponse, {
      'payment lookup after provider failure succeeded': (response) => response.status === 200,
    })

    check(finalPayment, {
      'payment remains Pending when provider submission never succeeded': (paymentResult) => paymentResult.status === 'Pending',
    })

    check(finalOrderResponse, {
      'order lookup after provider failure succeeded': (response) => response.status === 200,
    })

    check(finalOrder, {
      'order remains PendingPayment after provider failure': (orderResult) => orderResult.status === 'PendingPayment',
    })

    console.log(`InitialDlqMessages: ${data.initialDlqMessages}`)
    console.log(`FinalDlqMessages: ${finalDlqMessages}`)
    console.log(`PaymentId: ${payment.id}`)
    console.log(`FinalPaymentStatus: ${finalPayment.status}`)
    console.log(`FinalOrderStatus: ${finalOrder.status}`)
  })
}
