import type { Payment } from '../models/payment'

type CreatePaymentInput = {
  merchantOrderId: string
  amount: number
  currency: string
  correlationId: string
}

/**
 * Payment API 服务
 *
 * 所有和后端 Payment API 的通信都集中在这里，页面组件只调用业务动作：
 * 创建支付、查询支付。
 */
export async function createPayment(input: CreatePaymentInput) {
  const response = await fetch('/payments', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': input.correlationId,
    },
    body: JSON.stringify({
      merchantOrderId: input.merchantOrderId,
      amount: input.amount,
      currency: input.currency,
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
