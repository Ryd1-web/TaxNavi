'use client';

import { useCallback, useState } from 'react';
import { saveGuestReport } from '@/lib/guestReports';
import { apiService, ApiError } from '@/services/apiService';
import { CheckoutResult, TaxCalculationRequest } from '@/types';

export type CheckoutState =
  | { status: 'idle' }
  | { status: 'starting' }
  | { status: 'unavailable'; message: string }
  | { status: 'unlocked'; reportId?: string | null }
  | { status: 'error'; message: string };

export interface ReportCheckoutInput {
  taxReportId?: string | null;
  accessToken?: string | null;
  email?: string;
  calculation?: TaxCalculationRequest;
}

const PENDING_KEY = 'taxnavi.pendingCheckout';

export interface PendingCheckout {
  reference: string;
  purpose: 'PremiumReport' | 'SmeSubscription';
  taxReportId?: string | null;
  isGuest: boolean;
}

/**
 * Starts a server-side Paystack checkout and redirects to Paystack's hosted page.
 * The price comes from the server; nothing is unlocked here — only after the server verifies the payment.
 */
export function useCheckout() {
  const [state, setState] = useState<CheckoutState>({ status: 'idle' });

  const redirect = (checkout: CheckoutResult, pending: PendingCheckout) => {
    window.sessionStorage.setItem(PENDING_KEY, JSON.stringify(pending));
    window.location.assign(checkout.authorizationUrl);
  };

  const rememberGuest = (data: Partial<CheckoutResult> | undefined, email?: string) => {
    if (data?.taxReportId && data.accessToken) {
      saveGuestReport({ reportId: data.taxReportId, accessToken: data.accessToken, email: email ?? '' });
    }
  };

  const startReport = useCallback(async (input: ReportCheckoutInput, isGuest: boolean) => {
    setState({ status: 'starting' });
    try {
      const checkout = await apiService.checkout({
        purpose: 'PremiumReport',
        taxReportId: input.taxReportId ?? undefined,
        accessToken: input.accessToken ?? undefined,
        email: input.email,
        calculation: input.taxReportId ? undefined : input.calculation,
      });
      rememberGuest(checkout, input.email);
      redirect(checkout, { reference: checkout.reference, purpose: 'PremiumReport', taxReportId: checkout.taxReportId, isGuest });
    } catch (error) {
      const err = error as ApiError;
      rememberGuest(err.data as Partial<CheckoutResult> | undefined, input.email);
      if (err.code === 'PAYMENTS_NOT_CONFIGURED') setState({ status: 'unavailable', message: err.message });
      else if (err.code === 'ALREADY_UNLOCKED') setState({ status: 'unlocked', reportId: (err.data as CheckoutResult | undefined)?.taxReportId });
      else setState({ status: 'error', message: err.message });
    }
  }, []);

  const startSubscription = useCallback(async () => {
    setState({ status: 'starting' });
    try {
      const checkout = await apiService.subscriptionCheckout();
      redirect(checkout, { reference: checkout.reference, purpose: 'SmeSubscription', isGuest: false });
    } catch (error) {
      const err = error as ApiError;
      setState(err.code === 'PAYMENTS_NOT_CONFIGURED' ? { status: 'unavailable', message: err.message } : { status: 'error', message: err.message });
    }
  }, []);

  const reset = useCallback(() => setState({ status: 'idle' }), []);

  return { state, startReport, startSubscription, reset };
}

export function readPendingCheckout(): PendingCheckout | null {
  try {
    const raw = window.sessionStorage.getItem(PENDING_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function clearPendingCheckout() {
  window.sessionStorage.removeItem(PENDING_KEY);
}
