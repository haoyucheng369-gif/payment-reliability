import {
  CheckCircle2,
  CreditCard,
  Loader2,
  RefreshCw,
  RotateCcw,
  ShoppingCart,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import './App.css'

type PaymentStatus = 'Pending' | 'Processed' | 'Failed'

type Payment = {
  id: string
  merchantOrderId: string
  amount: number
  currency: string
  status: PaymentStatus
  correlationId: string
  createdAt: string
}

type ActivityItem = {
  id: string
  label: string
  value: string
}

const product = {
  name: 'Wireless Keyboard',
  description: 'Fixed checkout item for local idempotency testing.',
  quantity: 1,
  amount: 66.77,
  currency: 'EUR',
}

function createMerchantOrderId() {
  return `ORDER-${Date.now()}`
}

function createCorrelationId() {
  return `CORR-${crypto.randomUUID()}`
}

function formatMoney(amount: number, currency: string) {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
  }).format(amount)
}

function App() {
  const [merchantOrderId, setMerchantOrderId] = useState(createMerchantOrderId)
  const [payment, setPayment] = useState<Payment | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activity, setActivity] = useState<ActivityItem[]>([])
  const pollTimerRef = useRef<number | null>(null)

  const statusTone = payment?.status === 'Processed' ? 'complete' : 'pending'
  const hasPayment = payment !== null

  const total = useMemo(() => {
    return formatMoney(product.amount * product.quantity, product.currency)
  }, [])

  useEffect(() => {
    return () => {
      if (pollTimerRef.current) {
        window.clearInterval(pollTimerRef.current)
      }
    }
  }, [])

  async function createPayment() {
    setIsSubmitting(true)
    setError(null)

    const correlationId = createCorrelationId()

    try {
      const response = await fetch('/payments', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-Id': correlationId,
        },
        body: JSON.stringify({
          merchantOrderId,
          amount: product.amount,
          currency: product.currency,
        }),
      })

      if (!response.ok) {
        throw new Error(`Payment request failed with HTTP ${response.status}`)
      }

      const nextPayment = (await response.json()) as Payment
      setPayment(nextPayment)
      addActivity('POST /payments', nextPayment.id)
      startPolling(nextPayment.id)
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Payment request failed.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function refreshPayment(paymentId = payment?.id) {
    if (!paymentId) {
      return
    }

    try {
      const response = await fetch(`/payments/${paymentId}`)

      if (!response.ok) {
        throw new Error(`Payment lookup failed with HTTP ${response.status}`)
      }

      const nextPayment = (await response.json()) as Payment
      setPayment(nextPayment)

      if (nextPayment.status === 'Processed' && pollTimerRef.current) {
        window.clearInterval(pollTimerRef.current)
        pollTimerRef.current = null
        addActivity('GET /payments/{id}', 'Processed')
      }
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : 'Payment lookup failed.')
    }
  }

  function startPolling(paymentId: string) {
    if (pollTimerRef.current) {
      window.clearInterval(pollTimerRef.current)
    }

    // 后端通过 Worker 异步更新状态，前端只做轻量轮询展示结果。
    pollTimerRef.current = window.setInterval(() => {
      void refreshPayment(paymentId)
    }, 1000)
  }

  function addActivity(label: string, value: string) {
    setActivity((items) => [
      {
        id: crypto.randomUUID(),
        label,
        value,
      },
      ...items,
    ].slice(0, 5))
  }

  function startNewOrder() {
    if (pollTimerRef.current) {
      window.clearInterval(pollTimerRef.current)
      pollTimerRef.current = null
    }

    setMerchantOrderId(createMerchantOrderId())
    setPayment(null)
    setError(null)
    setActivity([])
  }

  return (
    <main className="app-shell">
      <section className="checkout-panel" aria-label="Checkout simulator">
        <header className="page-header">
          <div>
            <p className="eyebrow">PaymentFlowCloud checkout simulator</p>
            <h1>Idempotent payment creation</h1>
          </div>
          <div className={`status-pill ${statusTone}`}>
            {payment?.status === 'Processed' ? (
              <CheckCircle2 size={16} aria-hidden="true" />
            ) : (
              <Loader2 size={16} aria-hidden="true" />
            )}
            {payment?.status ?? 'Ready'}
          </div>
        </header>

        <section className="layout-grid">
          <section className="section-block order-summary" aria-label="Order summary">
            <div className="section-title">
              <ShoppingCart size={20} aria-hidden="true" />
              <h2>Order</h2>
            </div>

            <div className="product-row">
              <div>
                <strong>{product.name}</strong>
                <span>{product.description}</span>
              </div>
              <div className="price">{total}</div>
            </div>

            <dl className="data-list">
              <div>
                <dt>MerchantOrderId</dt>
                <dd>{merchantOrderId}</dd>
              </div>
              <div>
                <dt>Quantity</dt>
                <dd>{product.quantity}</dd>
              </div>
              <div>
                <dt>Total</dt>
                <dd>{total}</dd>
              </div>
            </dl>

            <div className="button-row">
              <button type="button" onClick={createPayment} disabled={isSubmitting}>
                <CreditCard size={16} aria-hidden="true" />
                Place Order
              </button>
              <button type="button" className="secondary" onClick={createPayment} disabled={isSubmitting}>
                <RefreshCw size={16} aria-hidden="true" />
                Click Again
              </button>
              <button type="button" className="ghost" onClick={startNewOrder}>
                <RotateCcw size={16} aria-hidden="true" />
                New Order
              </button>
            </div>
          </section>

          <section className="section-block payment-state" aria-label="Payment state">
            <div className="section-title">
              <CreditCard size={20} aria-hidden="true" />
              <h2>Payment</h2>
            </div>

            {hasPayment ? (
              <dl className="data-list compact">
                <div>
                  <dt>PaymentId</dt>
                  <dd>{payment.id}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{payment.status}</dd>
                </div>
                <div>
                  <dt>CorrelationId</dt>
                  <dd>{payment.correlationId}</dd>
                </div>
              </dl>
            ) : (
              <p className="empty-state">
                Submit the fixed order to create a payment. Reuse the same order to verify idempotency.
              </p>
            )}

            {error && <p className="error-state">{error}</p>}

            <div className="activity-log">
              <h3>Recent activity</h3>
              {activity.length > 0 ? (
                <ul>
                  {activity.map((item) => (
                    <li key={item.id}>
                      <span>{item.label}</span>
                      <code>{item.value}</code>
                    </li>
                  ))}
                </ul>
              ) : (
                <p>No requests yet.</p>
              )}
            </div>
          </section>
        </section>
      </section>
    </main>
  )
}

export default App
