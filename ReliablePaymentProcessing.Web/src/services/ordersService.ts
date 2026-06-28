import type { Order } from '../models/order'

type CreateOrderInput = {
  amount: number
  currency: string
}

/**
 * Order API 服务
 *
 * 创建订单代表真实业务里的“点击下单”；订单创建后，支付流程都围绕这个 OrderId 展开。
 */
export async function createOrder(input: CreateOrderInput) {
  const response = await fetch('/orders', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(input),
  })

  if (!response.ok) {
    throw new Error(`Order request failed with HTTP ${response.status}`)
  }

  return (await response.json()) as Order
}

export async function getOrder(orderId: string) {
  const response = await fetch(`/orders/${orderId}`)

  if (!response.ok) {
    throw new Error(`Order lookup failed with HTTP ${response.status}`)
  }

  return (await response.json()) as Order
}

