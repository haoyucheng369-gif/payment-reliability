/**
 * 支付模型
 *
 * Payment 现在基于 Order 创建；同一个 OrderId 重复 Pay 应该返回同一笔 Payment。
 */
export type PaymentStatus = 'Pending' | 'Processing' | 'Succeeded' | 'Failed'

export type Payment = {
  id: string
  orderId: string | null
  merchantOrderId: string
  amount: number
  currency: string
  status: PaymentStatus
  correlationId: string
  createdAt: string
}
