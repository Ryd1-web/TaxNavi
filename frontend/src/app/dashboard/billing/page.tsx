'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { Check, CreditCard } from '@/components/ui/Icons';
import { Alert, Badge, Card, CardHeader, EmptyState, PageHeader, Skeleton } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useApi } from '@/hooks/useApi';
import { useCheckout } from '@/hooks/useCheckout';
import { formatCurrency, formatDate } from '@/lib/formatters';
import { apiService, ApiError } from '@/services/apiService';
import { Entitlements, PaymentRecord, SubscriptionStatus } from '@/types';

const paymentTone: Record<PaymentRecord['status'], 'success' | 'warning' | 'danger' | 'neutral'> = {
  Completed: 'success',
  Pending: 'warning',
  Failed: 'danger',
  Cancelled: 'neutral',
  Expired: 'neutral',
};

const subscriptionCopy: Record<SubscriptionStatus, { label: string; tone: 'success' | 'warning' | 'danger' | 'neutral' }> = {
  Active: { label: 'Active', tone: 'success' },
  NonRenewing: { label: 'Cancelled — active until period end', tone: 'warning' },
  PastDue: { label: 'Payment failed', tone: 'danger' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
  Expired: { label: 'Expired', tone: 'neutral' },
};

export default function BillingPage() {
  const { user } = useAuth();
  const { state, startSubscription } = useCheckout();
  const { data, loading, error, reload } = useApi(async () => {
    const [entitlements, payments] = await Promise.all([apiService.entitlements(), apiService.paymentHistory()]);
    return { entitlements, payments };
  });

  const e = data?.entitlements;
  const isSme = user?.userType === 'SME';

  return (
    <div className="space-y-6">
      <PageHeader eyebrow="Account" title="Plan & billing" description="Your current plan, purchases and receipts." />

      {error && (
        <Alert tone="error">
          {error}{' '}
          <button type="button" className="link" onClick={reload}>
            Try again
          </button>
        </Alert>
      )}

      {loading || !e ? (
        <Skeleton className="h-48" />
      ) : (
        <>
          {!e.paymentsEnabled && (
            <Alert tone="info" title="Online payments are launching soon">
              You can keep calculating and saving reports for free. Unlocking full reports and the SME plan will be available here once payments go live.
            </Alert>
          )}
          {e.paymentsEnabled && e.testMode && (
            <Alert tone="warning" title="Payments are in test mode">
              Paystack is in test mode: use a Paystack test card. No real money is charged.
            </Alert>
          )}
          {e.subscriptionStatus === 'PastDue' && (
            <Alert tone="error" title="Your last renewal payment failed">
              Paystack will retry the charge. Update your card to avoid losing access after the {e.pricing.subscriptionGraceDays}-day grace period.
            </Alert>
          )}

          <div className="grid gap-6 lg:grid-cols-2">
            <CurrentPlan entitlements={e} onChanged={reload} />

            <Card className="p-5 sm:p-6">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-base font-semibold">SME plan</p>
                  <p className="mt-1 text-sm text-ink-subtle">For businesses that file regularly.</p>
                </div>
                <p className="text-right">
                  <span className="figure text-2xl font-semibold">{formatCurrency(e.pricing.smeSubscriptionPrice)}</span>
                  <span className="block text-xs text-ink-subtle">{e.subscriptionMode === 'Recurring' ? 'per month' : `per ${e.pricing.smeSubscriptionPeriodDays} days`}</span>
                </p>
              </div>
              <ul className="mt-4 space-y-2">
                {['Every saved report unlocked in full', 'PDF reports for your accountant', 'Compliance tracker & tax calendar'].map((item) => (
                  <li key={item} className="flex gap-2 text-sm">
                    <Check size={16} className="mt-0.5 shrink-0 text-brand-700" />
                    {item}
                  </li>
                ))}
              </ul>
              {!(e.hasActiveSubscription && e.subscriptionRecurring && e.subscriptionStatus !== 'NonRenewing') && (
                <Button
                  className="mt-5"
                  block
                  variant={isSme ? 'primary' : 'secondary'}
                  disabled={!e.paymentsEnabled || state.status === 'unavailable'}
                  loading={state.status === 'starting'}
                  loadingText="Opening secure checkout…"
                  onClick={startSubscription}
                >
                  {e.hasActiveSubscription && !e.subscriptionRecurring ? `Extend by ${e.pricing.smeSubscriptionPeriodDays} days` : 'Start SME plan'}
                </Button>
              )}
              {(state.status === 'error' || state.status === 'unavailable') && (
                <p className="mt-2 text-[13px] text-red-600" role="alert">
                  {state.message}
                </p>
              )}
              <p className="mt-3 text-[13px] text-ink-subtle">
                {e.subscriptionMode === 'Recurring'
                  ? 'Billed monthly by Paystack until you cancel. Your plan activates only after the first payment is confirmed.'
                  : 'Paid by Paystack. Your plan activates only after the payment is confirmed. It does not renew automatically.'}
              </p>
            </Card>
          </div>

          <Card>
            <CardHeader title="Payment history" />
            {data.payments.length === 0 ? (
              <EmptyState icon={<CreditCard />} title="No payments yet" description="Purchases and plan payments will appear here." />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[560px] text-sm">
                  <thead>
                    <tr className="border-b border-line text-left text-[13px] text-ink-subtle">
                      <th scope="col" className="px-5 py-3 font-medium sm:px-6">Date</th>
                      <th scope="col" className="px-3 py-3 font-medium">Item</th>
                      <th scope="col" className="px-3 py-3 font-medium">Reference</th>
                      <th scope="col" className="px-3 py-3 font-medium">Status</th>
                      <th scope="col" className="px-5 py-3 text-right font-medium sm:px-6">Amount</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.payments.map((payment) => (
                      <tr key={payment.reference} className="border-b border-line last:border-0">
                        <td className="px-5 py-3 sm:px-6">{formatDate(payment.paidAt ?? payment.createdAt)}</td>
                        <td className="px-3 py-3">{payment.purpose === 'SmeSubscription' ? 'SME plan' : 'Full tax report'}</td>
                        <td className="figure px-3 py-3 text-xs text-ink-subtle">{payment.reference.slice(0, 18)}…</td>
                        <td className="px-3 py-3">
                          <Badge tone={paymentTone[payment.status] ?? 'neutral'}>{payment.status}</Badge>
                        </td>
                        <td className="figure px-5 py-3 text-right font-medium sm:px-6">{formatCurrency(payment.amount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      )}
    </div>
  );
}

function CurrentPlan({ entitlements: e, onChanged }: { entitlements: Entitlements; onChanged: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState<'cancel' | 'card' | null>(null);
  const [message, setMessage] = useState<{ tone: 'success' | 'error'; text: string } | null>(null);
  const status = e.subscriptionStatus ? subscriptionCopy[e.subscriptionStatus] : null;

  const cancel = async () => {
    setBusy('cancel');
    setMessage(null);
    try {
      const result = await apiService.cancelSubscription();
      setMessage({ tone: 'success', text: result.message ?? 'Your plan will not renew.' });
      setConfirming(false);
      onChanged();
    } catch (err) {
      setMessage({ tone: 'error', text: (err as ApiError).message });
    } finally {
      setBusy(null);
    }
  };

  const updateCard = async () => {
    setBusy('card');
    setMessage(null);
    try {
      window.location.assign(await apiService.subscriptionManageLink());
    } catch (err) {
      setMessage({ tone: 'error', text: (err as ApiError).message });
      setBusy(null);
    }
  };

  return (
    <Card className="p-5 sm:p-6">
      <p className="eyebrow">Current plan</p>
      <div className="mt-2 flex flex-wrap items-center gap-2">
        <p className="text-xl font-semibold">{e.hasActiveSubscription ? 'SME plan' : 'Free'}</p>
        {e.hasActiveSubscription && status && <Badge tone={status.tone}>{status.label}</Badge>}
      </div>
      <p className="mt-1 text-sm text-ink-subtle">
        {e.hasActiveSubscription && e.subscriptionRecurring && e.subscriptionStatus === 'Active' && e.nextPaymentDate
          ? `Renews on ${formatDate(e.nextPaymentDate)}.`
          : e.hasActiveSubscription && e.subscriptionPeriodEnd
            ? `Access until ${formatDate(e.subscriptionPeriodEnd)}.`
            : `${e.unlockedReportCount} full ${e.unlockedReportCount === 1 ? 'report' : 'reports'} unlocked individually.`}
      </p>

      {message && (
        <Alert tone={message.tone} className="mt-4">
          {message.text}
        </Alert>
      )}

      {e.subscriptionRecurring && e.hasActiveSubscription && (
        <div className="mt-5 flex flex-wrap gap-2">
          <Button variant="secondary" size="sm" onClick={updateCard} loading={busy === 'card'}>
            Update card
          </Button>
          {e.canCancelSubscription &&
            (confirming ? (
              <>
                <Button variant="danger" size="sm" onClick={cancel} loading={busy === 'cancel'}>
                  Confirm cancellation
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setConfirming(false)}>
                  Keep plan
                </Button>
              </>
            ) : (
              <Button variant="ghost" size="sm" onClick={() => setConfirming(true)}>
                Cancel plan
              </Button>
            ))}
        </div>
      )}
      {confirming && <p className="mt-2 text-[13px] text-ink-subtle">You keep access until the end of the current billing period. No further charges.</p>}
    </Card>
  );
}
