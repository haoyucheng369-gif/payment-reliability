/**
 * ID 生成工具
 *
 * MerchantOrderId 用来模拟商户订单号，CorrelationId 用来串联一次请求链路。
 */
export function createMerchantOrderId() {
  return `ORDER-${Date.now()}`
}

export function createCorrelationId() {
  return `CORR-${crypto.randomUUID()}`
}

