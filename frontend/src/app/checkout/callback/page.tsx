'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Suspense, useEffect, useRef, useState } from 'react';
import PublicLayout from '@/components/layout/PublicLayout';
import { ButtonLink, Spinner } from '@/components/ui/Button';
import { AlertTriangle, CheckCircle, Clock, X } from '@/components/ui/Icons';
import { Card } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { clearPendingCheckout, readPendingCheckout } from '@/hooks/useCheckout';
import { apiService, ApiError } from '@/services/apiService';
import { PaymentRecord } from '@/types';

type Status = 'verifying' | 'success' | 'pending' | 'cancelled' | 'failed' | 'error';

const MAX_ATTEMPTS = 8;

/**
 * Paystack returns here (?reference=… after paying, or ?reference=…&cancelled=1 if the customer cancels).
 * The server verifies the transaction with Paystack — nothing is treated as paid until it confirms.
 */
function CallbackInner() {
  const params = useSearchParams();
  const { isAuthenticated, isLoading } = useAuth();
  const [status, setStatus] = useState<Status>('verifying');
  const [payment, setPayment] = useState<PaymentRecord | null>(null);
  const [message, setMessage] = useState('');
  const attempts = useRef(0);

  const pending = typeof window === 'undefined' ? null : readPendingCheckout();
  const reference = params.get('reference') || params.get('trxref') || pending?.reference || '';
  const cancelledByCustomer = params.get('cancelled') === '1';

  useEffect(() => {
    if (isLoading || !reference) return;
    let timer: number | undefined;
    let cancelled = false;

    const verify = async () => {
      attempts.current += 1;
      try {
        const response = await apiService.verifyPayment(reference);
        if (cancelled) return;
        const body = response.data;
        setPayment(body.data ?? null);
        setMessage(body.message ?? '');

        if (response.status === 200 && body.success) {
          clearPendingCheckout();
          setStatus('success');
        } else if (body.code === 'PAYMENT_CANCELLED' || (cancelledByCustomer && response.status === 202)) {
          setStatus('cancelled');
        } else if (response.status === 202) {
          setStatus('pending');
          if (attempts.current < MAX_ATTEMPTS) timer = window.setTimeout(verify, 2500 + attempts.current * 500);
        } else if (response.status === 404) {
          setStatus('error');
          setMessage(isAuthenticated ? 'We could not find this payment on your account.' : 'We could not find this payment. If you paid while signed in, sign in to confirm it.');
        } else {
          setStatus('failed');
        }
      } catch (err) {
        if (!cancelled) {
          setStatus('error');
          setMessage((err as ApiError).message);
        }
      }
    };

    verify();
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [isLoading, isAuthenticated, reference, cancelledByCustomer]);

  const reportHref = payment?.taxReportId
    ? payment.isGuest
      ? `/report/${payment.taxReportId}`
      : `/dashboard/reports/${payment.taxReportId}`
    : isAuthenticated
      ? '/dashboard'
      : '/';
  const retryHref = payment?.taxReportId ? reportHref : '/calculate';

  if (!reference) {
    return (
      <Shell icon={<AlertTriangle className="text-amber-600" />} title="No payment to confirm">
        <p>We couldn&apos;t find a payment reference. If you were charged, contact support with your Paystack receipt.</p>
        <ButtonLink href="/" className="mt-6">
          Go home
        </ButtonLink>
      </Shell>
    );
  }

  if (status === 'verifying' || (status === 'pending' && attempts.current < MAX_ATTEMPTS)) {
    return (
      <Shell icon={<Spinner className="h-6 w-6 text-brand-700" />} title="Confirming your payment…">
        <p>We&apos;re checking with Paystack. This usually takes a few seconds — please keep this page open.</p>
      </Shell>
    );
  }

  if (status === 'success') {
    return (
      <Shell icon={<CheckCircle className="text-brand-700" />} title="Payment confirmed">
        <p>{payment?.purpose === 'SmeSubscription' ? 'Your SME plan is now active.' : 'Your full tax report is unlocked.'}</p>
        {payment?.isGuest && <p className="mt-2 text-[13px]">Your report is saved on this device. Create a free account to keep it safe across devices.</p>}
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <ButtonLink href={reportHref}>{payment?.purpose === 'SmeSubscription' ? 'Go to dashboard' : 'View my report'}</ButtonLink>
          {payment?.isGuest && !isAuthenticated && (
            <ButtonLink href={`/auth/register?next=${encodeURIComponent(reportHref)}`} variant="secondary">
              Create account
            </ButtonLink>
          )}
        </div>
      </Shell>
    );
  }

  if (status === 'cancelled') {
    return (
      <Shell icon={<X className="text-ink-muted" />} title="Payment cancelled">
        <p>You cancelled the payment, so you haven&apos;t been charged. Your results are still available.</p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <ButtonLink href={retryHref}>Back to my report</ButtonLink>
        </div>
      </Shell>
    );
  }

  if (status === 'pending') {
    return (
      <Shell icon={<Clock className="text-amber-600" />} title="Payment still processing">
        <p>Your bank hasn&apos;t confirmed the payment yet. We&apos;ll unlock your purchase automatically as soon as Paystack confirms it — you can check back shortly.</p>
        <Reference value={reference} />
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button type="button" className="btn btn-primary" onClick={() => window.location.reload()}>
            Check again
          </button>
          <Link href={reportHref} className="btn btn-secondary">
            Back to my report
          </Link>
        </div>
      </Shell>
    );
  }

  return (
    <Shell icon={<AlertTriangle className="text-red-600" />} title={status === 'failed' ? 'Payment not completed' : 'We couldn’t confirm this payment'}>
      <p>{message || 'The payment did not go through. No money was taken — you can try again.'}</p>
      <Reference value={reference} />
      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <ButtonLink href={retryHref}>Try again</ButtonLink>
        {!isAuthenticated && status === 'error' && (
          <ButtonLink href={`/auth/login?next=${encodeURIComponent(`/checkout/callback?reference=${reference}`)}`} variant="secondary">
            Sign in
          </ButtonLink>
        )}
      </div>
    </Shell>
  );
}

function Reference({ value }: { value: string }) {
  return (
    <p className="mt-3 text-[13px]">
      Reference: <span className="figure break-all">{value}</span>
    </p>
  );
}

function Shell({ icon, title, children }: { icon: React.ReactNode; title: string; children: React.ReactNode }) {
  return (
    <div className="container-page flex justify-center py-16 sm:py-24">
      <Card className="w-full max-w-md p-8 text-center">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-surface-muted" aria-hidden="true">
          {icon}
        </div>
        <h1 className="mt-5 text-xl font-semibold" aria-live="polite">
          {title}
        </h1>
        <div className="mt-2 text-[15px] leading-6 text-ink-muted">{children}</div>
      </Card>
    </div>
  );
}

export default function CheckoutCallbackPage() {
  return (
    <PublicLayout footer={false}>
      <Suspense>
        <CallbackInner />
      </Suspense>
    </PublicLayout>
  );
}
