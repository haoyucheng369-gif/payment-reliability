import { useCallback, useEffect, useRef, useState } from 'react'
import { checkoutProduct } from '../data/checkoutProduct'
import type { ActivityItem } from '../models/activity'
import type { Order } from '../models/order'
import type { Payment } from '../models/payment'
import { createOrder } from '../services/ordersService'
import { createPayment, getPayment } from '../services/paymentsService'
import { createCorrelationId } from '../utils/ids'

/**
 * 结账支付流程 Hook
 *
 * 管理真实一点的本地流程：Create Order -> Pay -> Provider webhook 更新支付状态。
 */
export function useCheckoutPayment() {
  const [order, setOrder] = useState<Order | null>(null)
  const [payment, setPayment] = useState<Payment | null>(null)
  const [isCreatingOrder, setIsCreatingOrder] = useState(false)
  const [isSubmittingPayment, setIsSubmittingPayment] = useState(false)
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

        if (nextPayment.status === 'Succeeded') {
          stopPolling()
          addActivity('GET /payments/{id}', 'Succeeded')
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

      // 后端通过 Worker 和 provider webhook 异步更新状态，前端只做轻量轮询展示 Pending -> Processing -> Succeeded。
      pollTimerRef.current = window.setInterval(() => {
        void refreshPayment(paymentId)
      }, 1000)
    },
    [refreshPayment, stopPolling],
  )

  const submitOrder = useCallback(async () => {
    stopPolling()
    setIsCreatingOrder(true)
    setError(null)
    setPayment(null)

    try {
      // 点击下单才创建 Order；刷新页面不会自动生成新订单。
      const nextOrder = await createOrder({
        amount: checkoutProduct.amount,
        currency: checkoutProduct.currency,
      })

      setOrder(nextOrder)
      setActivity([])
      addActivity('POST /orders', nextOrder.id)
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Order request failed.')
    } finally {
      setIsCreatingOrder(false)
    }
  }, [addActivity, stopPolling])

  const submitPayment = useCallback(async () => {
    if (!order) {
      setError('Create an order before paying.')
      return
    }

    setIsSubmittingPayment(true)
    setError(null)

    try {
      /**
       * 同一个 OrderId 重复 Pay 会返回同一笔 Payment。
       * 每次 HTTP 请求仍然生成新的 CorrelationId，方便后续链路追踪。
       */
      const nextPayment = await createPayment({
        orderId: order.id,
        correlationId: createCorrelationId(),
      })

      setPayment(nextPayment)
      addActivity('POST /payments', nextPayment.id)
      startPolling(nextPayment.id)
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Payment request failed.')
    } finally {
      setIsSubmittingPayment(false)
    }
  }, [addActivity, order, startPolling])

  const startNewOrder = useCallback(() => {
    stopPolling()
    setOrder(null)
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
    order,
    payment,
    isCreatingOrder,
    isSubmittingPayment,
    error,
    activity,
    submitOrder,
    submitPayment,
    startNewOrder,
  }
}
