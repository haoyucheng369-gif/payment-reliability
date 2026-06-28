import type { Payment } from '../models/payment'

type CreatePaymentInput = {
  orderId: string
  correlationId: string
}

/**
 * Payment API 服务
 *
 * 支付创建只传 OrderId，金额和商户订单号由后端从 Order 读取，避免前端篡改支付金额。
 */
export async function createPayment(input: CreatePaymentInput) {
  const response = await fetch('/payments', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': input.correlationId,
    },
    body: JSON.stringify({
      orderId: input.orderId,
    }),
  })

  if (!response.ok) {
    throw new Error(`Payment request failed with HTTP ${response.status}`)
  }

  return (await response.json()) as Payment
}

export async function getPayment(paymentId: string) {
  const response = await fetch(`/payments/${paymentId}`)

  if (!response.ok) {
    throw new Error(`Payment lookup failed with HTTP ${response.status}`)
  }

  return (await response.json()) as Payment
}

