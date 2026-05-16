export type OrderStatus = 'PendingPayment' | 'Paid' | 'Cancelled'

/**
 * 订单模型
 *
 * Order 代表真实业务订单，刷新页面不应该随便变化；当前页面通过 Create Order 显式创建。
 */
export type Order = {
  id: string
  merchantOrderId: string
  amount: number
  currency: string
  status: OrderStatus
  createdAt: string
}

export type CheckoutProduct = {
  name: string
  description: string
  quantity: number
  amount: number
  currency: string
}

