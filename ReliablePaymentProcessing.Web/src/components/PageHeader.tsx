import { CheckCircle2, Loader2 } from 'lucide-react'
import type { Payment } from '../models/payment'

type PageHeaderProps = {
  payment: Payment | null
}

/**
 * 页面顶部状�? *
 * 只展示当前支付流程的全局状态，不负责请求或业务判断�? */
export function PageHeader({ payment }: PageHeaderProps) {
  const isSucceeded = payment?.status === 'Succeeded'
  const statusClassName = isSucceeded
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : 'border-slate-200 bg-white text-slate-600'

  return (
    <header className="mb-7 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <p className="mb-2 text-xs font-semibold uppercase text-slate-500">ReliablePaymentProcessing checkout simulator</p>
        <h1 className="text-3xl font-semibold leading-tight text-slate-950 sm:text-4xl">Idempotent payment creation</h1>
      </div>
      <div
        className={`inline-flex min-h-9 w-fit items-center gap-2 rounded-full border px-3.5 text-sm font-semibold ${statusClassName}`}
      >
        {isSucceeded ? (
          <CheckCircle2 size={16} aria-hidden="true" />
        ) : (
          <Loader2 size={16} className={payment ? 'animate-spin' : undefined} aria-hidden="true" />
        )}
        {payment?.status ?? 'Ready'}
      </div>
    </header>
  )
}
