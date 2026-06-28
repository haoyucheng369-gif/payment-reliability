import type { CheckoutProduct } from '../models/order'

/**
 * 固定结账商品
 *
 * 现在不做完整电商购物车，先用固定商品专注训练支付创建、幂等和异步状态更新。
 */
export const checkoutProduct: CheckoutProduct = {
  name: 'Wireless Keyboard',
  description: 'Fixed checkout item for local idempotency testing.',
  quantity: 1,
  amount: 66.77,
  currency: 'EUR',
}
