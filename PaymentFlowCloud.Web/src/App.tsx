import { useMemo } from 'react'
import { OrderSummary } from './components/OrderSummary'
import { PageHeader } from './components/PageHeader'
import { PaymentState } from './components/PaymentState'
import { checkoutProduct } from './data/checkoutProduct'
import { useCheckoutPayment } from './hooks/useCheckoutPayment'
import { formatMoney } from './utils/money'

/**
 * 支付模拟页面
 *
 * App 只负责组合页面结构，具体下单和支付流程交给 useCheckoutPayment。
 */
function App() {
  const {
    order,
    payment,
    isCreatingOrder,
    isSubmittingPayment,
    error,
    activity,
    submitOrder,
    submitPayment,
    startNewOrder,
  } = useCheckoutPayment()

  const total = useMemo(() => {
    return formatMoney(checkoutProduct.amount * checkoutProduct.quantity, checkoutProduct.currency)
  }, [])

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-8 text-slate-900 sm:px-6 lg:py-12">
      <section className="mx-auto w-full max-w-6xl" aria-label="Checkout simulator">
        <PageHeader payment={payment} />

        <section className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(340px,0.72fr)]">
          <OrderSummary
            order={order}
            total={total}
            isCreatingOrder={isCreatingOrder}
            isSubmittingPayment={isSubmittingPayment}
            hasPayment={payment !== null}
            onCreateOrder={submitOrder}
            onCreatePayment={submitPayment}
            onStartNewOrder={startNewOrder}
          />

          <PaymentState payment={payment} error={error} activity={activity} />
        </section>
      </section>
    </main>
  )
}

export default App
