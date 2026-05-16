import { CreditCard, RefreshCw, RotateCcw, ShoppingCart } from 'lucide-react'
import { checkoutProduct } from '../data/checkoutProduct'

type OrderSummaryProps = {
  merchantOrderId: string
  total: string
  isSubmitting: boolean
  onCreatePayment: () => void
  onStartNewOrder: () => void
}

/**
 * 订单摘要和操作按钮
 *
 * Place Order 和 Click Again 都会提交同一个 MerchantOrderId。
 * Click Again 专门模拟重复点击，用来验证后端幂等逻辑。
 */
export function OrderSummary({
  merchantOrderId,
  total,
  isSubmitting,
  onCreatePayment,
  onStartNewOrder,
}: OrderSummaryProps) {
  const primaryButtonClassName =
    'inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-sky-700 bg-sky-700 px-4 text-sm font-semibold text-white transition hover:bg-sky-800 disabled:cursor-wait disabled:opacity-70'
  const secondaryButtonClassName =
    'inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-slate-300 bg-white px-4 text-sm font-semibold text-slate-800 transition hover:bg-slate-100 disabled:cursor-wait disabled:opacity-70'
  const ghostButtonClassName =
    'inline-flex min-h-10 items-center justify-center gap-2 rounded-md border border-transparent px-4 text-sm font-semibold text-sky-700 transition hover:bg-sky-50'

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm sm:p-6" aria-label="Order summary">
      <div className="mb-5 flex items-center gap-2.5 text-sky-700">
        <ShoppingCart size={20} aria-hidden="true" />
        <h2 className="text-lg font-semibold text-slate-900">Order</h2>
      </div>

      <div className="flex flex-col gap-3 border-y border-slate-100 py-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <strong className="block text-base font-semibold text-slate-950">{checkoutProduct.name}</strong>
          <span className="block text-sm leading-6 text-slate-500">{checkoutProduct.description}</span>
        </div>
        <div className="whitespace-nowrap text-lg font-bold text-slate-950">{total}</div>
      </div>

      <dl className="my-5 grid gap-3">
        <div className="grid gap-1 sm:grid-cols-[142px_minmax(0,1fr)] sm:gap-3">
          <dt className="text-sm font-semibold text-slate-500">MerchantOrderId</dt>
          <dd className="min-w-0 break-words font-mono text-sm text-slate-800">{merchantOrderId}</dd>
        </div>
        <div className="grid gap-1 sm:grid-cols-[142px_minmax(0,1fr)] sm:gap-3">
          <dt className="text-sm font-semibold text-slate-500">Quantity</dt>
          <dd className="min-w-0 font-mono text-sm text-slate-800">{checkoutProduct.quantity}</dd>
        </div>
        <div className="grid gap-1 sm:grid-cols-[142px_minmax(0,1fr)] sm:gap-3">
          <dt className="text-sm font-semibold text-slate-500">Total</dt>
          <dd className="min-w-0 font-mono text-sm text-slate-800">{total}</dd>
        </div>
      </dl>

      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
        <button type="button" className={primaryButtonClassName} onClick={onCreatePayment} disabled={isSubmitting}>
          <CreditCard size={16} aria-hidden="true" />
          Place Order
        </button>
        <button type="button" className={secondaryButtonClassName} onClick={onCreatePayment} disabled={isSubmitting}>
          <RefreshCw size={16} aria-hidden="true" />
          Click Again
        </button>
        <button type="button" className={ghostButtonClassName} onClick={onStartNewOrder}>
          <RotateCcw size={16} aria-hidden="true" />
          New Order
        </button>
      </div>
    </section>
  )
}
