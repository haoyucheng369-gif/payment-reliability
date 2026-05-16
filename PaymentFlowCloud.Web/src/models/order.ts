/**
 * 订单模型
 *
 * 当前页面只模拟一个固定商品的支付流程，后续如果扩展购物车，
 * 可以先从这个模型继续演进。
 */
export type CheckoutProduct = {
  name: string
  description: string
  quantity: number
  amount: number
  currency: string
}

