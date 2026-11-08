'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Suspense, useCallback, useEffect, useRef, useState } from 'react';
import PublicLayout from '@/components/layout/PublicLayout';
import CalculatorWizard from '@/components/tax/CalculatorWizard';
import TaxResults from '@/components/tax/TaxResults';
import { Button } from '@/components/ui/Button';
import { Alert, Skeleton } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useTaxRules } from '@/hooks/useTaxRules';
import {
  buildRequest,
  CalculatorState,
  Category,
  clearProgress,
  initialState,
  loadProgress,
  saveProgress,
  StepId,
  stepsFor,
} from '@/lib/calculator';
import { formatDate } from '@/lib/formatters';
import { apiService, ApiError } from '@/services/apiService';
import { TaxReportAccessResponse } from '@/types';

const TYPE_TO_CATEGORY: Record<string, Category> = {
  individual: 'employee',
  employee: 'employee',
  'self-employed': 'self-employed',
  freelancer: 'self-employed',
  sme: 'business',
  business: 'business',
};

function CalculatePageInner() {
  const params = useSearchParams();
  const router = useRouter();
  const { isAuthenticated, isLoading: authLoading } = useAuth();
  const { rules, error: rulesError } = useTaxRules();

  const [state, setState] = useState<CalculatorState | null>(null);
  const [step, setStep] = useState<StepId>('category');
  const [result, setResult] = useState<TaxReportAccessResponse | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resumeOffer, setResumeOffer] = useState<{ savedAt: string } | null>(null);
  const resumed = useRef(false);
  const { rules: yearRules } = useTaxRules(state?.taxYear);

  // Initialise once rules (supported years) are known.
  useEffect(() => {
    if (!rules || state) return;
    const preset = TYPE_TO_CATEGORY[params.get('type') ?? ''] ?? '';
    const saved = loadProgress();
    const wantsResume = params.get('resume') === '1';

    if (saved && wantsResume) {
      setState(saved.state);
      setStep(saved.step);
      return;
    }

    const fresh = initialState(rules.defaultTaxYear, preset);
    setState(fresh);
    setStep(preset ? stepsFor(fresh)[1] : 'category');
    if (saved && saved.state.category) setResumeOffer({ savedAt: saved.savedAt });
  }, [rules, state, params]);

  // Persist progress locally on every change.
  useEffect(() => {
    if (state) saveProgress(state, step);
  }, [state, step]);

  const calculate = useCallback(async (current: CalculatorState, saveToAccount: boolean) => {
    setSubmitting(true);
    setError(null);
    try {
      const request = buildRequest(current);
      const response = saveToAccount ? await apiService.saveReport(request) : await apiService.calculateFree(request);
      setResult(response);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (err) {
      setError((err as ApiError).message);
      setStep('review');
    } finally {
      setSubmitting(false);
    }
  }, []);

  // Returning from sign-up/sign-in: recalculate and save straight to the new account.
  useEffect(() => {
    if (resumed.current || authLoading || !state || params.get('resume') !== '1' || step !== 'review') return;
    resumed.current = true;
    calculate(state, isAuthenticated);
    router.replace('/calculate', { scroll: false });
  }, [authLoading, state, step, params, isAuthenticated, calculate, router]);

  const onChange = (patch: Partial<CalculatorState>) => setState((current) => (current ? { ...current, ...patch } : current));

  const onSubmit = () => {
    if (!state) return;
    setStep('review');
    calculate(state, false);
  };

  const onSave = async () => {
    if (!state) return;
    const response = await apiService.saveReport(buildRequest(state));
    setResult(response);
  };

  const restart = () => {
    clearProgress();
    if (!rules) return;
    setResult(null);
    setResumeOffer(null);
    setState(initialState(rules.defaultTaxYear));
    setStep('category');
  };

  if (rulesError) {
    return (
      <div className="container-page py-16">
        <Alert tone="error" title="We couldn't load the tax rules">
          {rulesError} <button className="link" onClick={() => window.location.reload()}>Try again</button>
        </Alert>
      </div>
    );
  }

  if (!state || !rules) {
    return (
      <div className="container-page py-10" aria-busy="true">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="mt-6 h-96 w-full" />
        <span className="sr-only">Loading calculator…</span>
      </div>
    );
  }

  if (result) {
    return (
      <div className="container-page py-8 sm:py-10">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="eyebrow">Your estimate</p>
            <h1 className="mt-1 text-2xl font-semibold sm:text-[28px]">Here&apos;s your tax position</h1>
          </div>
          <Button variant="ghost" size="sm" onClick={restart}>
            Start a new calculation
          </Button>
        </div>
        <TaxResults
          response={result}
          calculation={buildRequest(state)}
          onSave={isAuthenticated ? onSave : undefined}
          onEdit={() => {
            setResult(null);
            setStep('review');
          }}
        />
      </div>
    );
  }

  return (
    <div className="container-page py-8 sm:py-12">
      <div className="mb-8 max-w-2xl">
        <p className="eyebrow">Free tax calculator</p>
        <h1 className="mt-1 text-2xl font-semibold sm:text-[32px]">Calculate your Nigerian tax</h1>
        <p className="mt-2 text-[15px] leading-6 text-ink-muted">About 3 minutes. No account needed — your answers stay on this device.</p>
      </div>

      {resumeOffer && (
        <Alert tone="info" className="mb-6" onDismiss={() => setResumeOffer(null)}>
          You have an unfinished calculation from {formatDate(resumeOffer.savedAt)}.{' '}
          <button
            type="button"
            className="link"
            onClick={() => {
              const saved = loadProgress();
              if (saved) {
                setState(saved.state);
                setStep(saved.step);
              }
              setResumeOffer(null);
            }}
          >
            Continue where you left off
          </button>
        </Alert>
      )}

      <CalculatorWizard
        state={state}
        step={step}
        rules={yearRules?.taxYear === state.taxYear ? yearRules : null}
        supportedYears={rules.supportedTaxYears}
        onChange={onChange}
        onStepChange={setStep}
        onSubmit={onSubmit}
        submitting={submitting}
        error={error}
      />

      {stepsFor(state).indexOf(step) > 0 && (
        <p className="mt-6 text-center text-[13px] text-ink-subtle">
          <button type="button" className="underline-offset-4 hover:underline" onClick={restart}>
            Clear my answers and start again
          </button>
        </p>
      )}
    </div>
  );
}

export default function CalculatePage() {
  return (
    <PublicLayout footer={false}>
      <Suspense fallback={<div className="container-page py-10"><Skeleton className="h-96 w-full" /></div>}>
        <CalculatePageInner />
      </Suspense>
    </PublicLayout>
  );
}
