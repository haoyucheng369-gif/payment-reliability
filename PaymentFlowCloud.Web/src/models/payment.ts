/**
 * 支付模型
 *
 * 这些类型和后端 Payment API 的返回结构保持一致，组件只依赖模型，
 * 不直接关心后端 Controller 或数据库实现。
 */
export type PaymentStatus = 'Pending' | 'Processed' | 'Failed'

export type Payment = {
  id: string
  merchantOrderId: string
  amount: number
  currency: string
  status: PaymentStatus
  correlationId: string
  createdAt: string
}
