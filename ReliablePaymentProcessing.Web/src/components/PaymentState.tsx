import { CreditCard } from 'lucide-react'
import type { ActivityItem } from '../models/activity'
import type { Payment } from '../models/payment'

type PaymentStateProps = {
  payment: Payment | null
  error: string | null
  activity: ActivityItem[]
}

/**
 * 支付结果和请求日志
 *
 * 只展示 API 返回结果和最近请求记录，方便观察 Pending 到 Processing 再到 Succeeded 的异步变化。
 */
export function PaymentState({ payment, error, activity }: PaymentStateProps) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm sm:p-6" aria-label="Payment state">
      <div className="mb-5 flex items-center gap-2.5 text-sky-700">
        <CreditCard size={20} aria-hidden="true" />
        <h2 className="text-lg font-semibold text-slate-900">Payment</h2>
      </div>

      {payment ? (
        <dl className="my-5 grid gap-3">
          <div className="grid gap-1 sm:grid-cols-[98px_minmax(0,1fr)] sm:gap-3">
            <dt className="text-sm font-semibold text-slate-500">PaymentId</dt>
            <dd className="min-w-0 break-words font-mono text-sm text-slate-800">{payment.id}</dd>
          </div>
          <div className="grid gap-1 sm:grid-cols-[98px_minmax(0,1fr)] sm:gap-3">
            <dt className="text-sm font-semibold text-slate-500">OrderId</dt>
            <dd className="min-w-0 break-words font-mono text-sm text-slate-800">{payment.orderId}</dd>
          </div>
          <div className="grid gap-1 sm:grid-cols-[98px_minmax(0,1fr)] sm:gap-3">
            <dt className="text-sm font-semibold text-slate-500">Status</dt>
            <dd className="min-w-0 font-mono text-sm text-slate-800">{payment.status}</dd>
          </div>
          <div className="grid gap-1 sm:grid-cols-[98px_minmax(0,1fr)] sm:gap-3">
            <dt className="text-sm font-semibold text-slate-500">CorrelationId</dt>
            <dd className="min-w-0 break-words font-mono text-sm text-slate-800">{payment.correlationId}</dd>
          </div>
        </dl>
      ) : (
        <p className="text-sm leading-6 text-slate-500">Create an order first, then pay from that OrderId.</p>
      )}

      {error && <p className="mt-4 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}

      <div className="mt-6 border-t border-slate-100 pt-5">
        <h3 className="text-sm font-semibold text-slate-700">Recent activity</h3>
        {activity.length > 0 ? (
          <ul className="mt-3 grid gap-2.5">
            {activity.map((item) => (
              <li key={item.id} className="grid gap-1 text-sm text-slate-500 sm:grid-cols-[140px_minmax(0,1fr)] sm:gap-3">
                <span>{item.label}</span>
                <code className="min-w-0 break-words rounded bg-slate-100 px-1.5 py-1 font-mono text-sm text-slate-800">
                  {item.value}
                </code>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-3 text-sm leading-6 text-slate-500">No requests yet.</p>
        )}
      </div>
    </section>
  )
}
