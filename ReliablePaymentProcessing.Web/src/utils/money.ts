/**
 * 金额格式化工具
 *
 * 页面展示金额时统一从这里处理，避免组件里重复写 Intl 配置。
 */
export function formatMoney(amount: number, currency: string) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
  }).format(amount)
}

