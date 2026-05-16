import { useCallback, useEffect, useRef, useState } from 'react'
import { checkoutProduct } from '../data/checkoutProduct'
import type { ActivityItem } from '../models/activity'
import type { Payment } from '../models/payment'
import { createPayment, getPayment } from '../services/paymentsService'
import { createCorrelationId, createMerchantOrderId } from '../utils/ids'

/**
 * 结账支付流程 Hook
 *
 * 这里集中管理支付页面的业务状态：
 * 当前订单号、支付结果、提交状态、错误信息、最近请求日志和轮询定时器。
 */
export function useCheckoutPayment() {
  const [merchantOrderId, setMerchantOrderId] = useState(createMerchantOrderId)
  const [payment, setPayment] = useState<Payment | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activity, setActivity] = useState<ActivityItem[]>([])
  const pollTimerRef = useRef<number | null>(null)

  const stopPolling = useCallback(() => {
    if (pollTimerRef.current) {
      window.clearInterval(pollTimerRef.current)
      pollTimerRef.current = null
    }
  }, [])

  /**
   * 记录最近请求
   *
   * 只保留最近 5 条，避免本地测试时重复点击导致页面日志无限增长。
   */
  const addActivity = useCallback((label: string, value: string) => {
    setActivity((items) =>
      [
        {
          id: crypto.randomUUID(),
          label,
          value,
        },
        ...items,
      ].slice(0, 5),
    )
  }, [])

  const refreshPayment = useCallback(
    async (paymentId: string) => {
      try {
        const nextPayment = await getPayment(paymentId)
        setPayment(nextPayment)

        if (nextPayment.status === 'Processed') {
          stopPolling()
          addActivity('GET /payments/{id}', 'Processed')
        }
      } catch (ex) {
        setError(ex instanceof Error ? ex.message : 'Payment lookup failed.')
      }
    },
    [addActivity, stopPolling],
  )

  const startPolling = useCallback(
    (paymentId: string) => {
      stopPolling()

      // 后端通过 Worker 异步更新状态，前端只做轻量轮询展示 Pending -> Processed。
      pollTimerRef.current = window.setInterval(() => {
        void refreshPayment(paymentId)
      }, 1000)
    },
    [refreshPayment, stopPolling],
  )

  const submitPayment = useCallback(async () => {
    setIsSubmitting(true)
    setError(null)

    try {
      /**
       * 每次 HTTP 请求都有新的 CorrelationId。
       *
       * MerchantOrderId 保持不变时，后端会通过唯一约束和幂等服务返回同一笔 Payment。
       */
      const nextPayment = await createPayment({
        merchantOrderId,
        amount: checkoutProduct.amount,
        currency: checkoutProduct.currency,
        correlationId: createCorrelationId(),
      })

      setPayment(nextPayment)
      addActivity('POST /payments', nextPayment.id)
      startPolling(nextPayment.id)
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Payment request failed.')
    } finally {
      setIsSubmitting(false)
    }
  }, [addActivity, merchantOrderId, startPolling])

  const startNewOrder = useCallback(() => {
    stopPolling()
    setMerchantOrderId(createMerchantOrderId())
    setPayment(null)
    setError(null)
    setActivity([])
  }, [stopPolling])

  useEffect(() => {
    return () => {
      stopPolling()
    }
  }, [stopPolling])

  return {
    merchantOrderId,
    payment,
    isSubmitting,
    error,
    activity,
    submitPayment,
    startNewOrder,
  }
}
