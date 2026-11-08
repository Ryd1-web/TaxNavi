'use client';

import Link from 'next/link';
import { FormEvent, ReactNode, useEffect, useId, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { useCheckout } from '@/hooks/useCheckout';
import { apiService, ApiError } from '@/services/apiService';
import { formatCurrency, formatDate, formatPercent, taxpayerLabel } from '@/lib/formatters';
import { FullTaxReport, PaymentsConfig, TaxCalculationRequest, TaxReportAccessResponse } from '@/types';
import { Button, ButtonLink } from '@/components/ui/Button';
import { AlertTriangle, CheckCircle, Download, FileText, Info, Lock, Shield } from '@/components/ui/Icons';
import { Alert, Badge, Card, Stat, VerificationBadge } from '@/components/ui/Primitives';
import AssistantPanel from './AssistantPanel';
import BandBreakdown from './BandBreakdown';
import IncomeBreakdownBar from './IncomeBreakdownBar';

interface Props {
  response: TaxReportAccessResponse;
  calculation?: TaxCalculationRequest;
  createdAt?: string;
  /** Saves the current calculation to the signed-in user's account. */
  onSave?: () => Promise<void>;
  onEdit?: () => void;
  resumeHref?: string;
  /** Set when showing a guest (no-account) report; proves access for PDF download and checkout. */
  guestAccessToken?: string;
}

export default function TaxResults({ response, calculation, createdAt, onSave, onEdit, resumeHref = '/calculate?resume=1', guestAccessToken }: Props) {
  const { freeSummary: s, fullReport, premiumGate, accessGranted } = response;
  const isCompany = s.taxType === 'CompanyIncomeTax';

  return (
    <div className="space-y-6">
      <ResultHero response={response} createdAt={createdAt} onEdit={onEdit} />

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="min-w-0 space-y-6">
          <Card className="p-5 sm:p-6">
            <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
              <Stat label={isCompany ? 'Turnover & income' : 'Gross income'} value={formatCurrency(s.grossIncome)} />
              <Stat label={isCompany ? 'Expenses' : 'Reliefs & deductions'} value={formatCurrency(s.totalDeductionsAndReliefs)} />
              <Stat label={isCompany ? 'Assessable profit' : 'Taxable income'} value={formatCurrency(s.basicTaxableIncome)} />
              <Stat label="Effective tax rate" value={formatPercent(s.effectiveTaxRate, 1)} sub="Tax ÷ gross income" />
            </div>
            <div className="mt-6 border-t border-line pt-6">
              <IncomeBreakdownBar grossIncome={s.grossIncome} taxableIncome={s.basicTaxableIncome} taxPayable={s.estimatedTaxPayable} isCompany={isCompany} />
            </div>
          </Card>

          {/* On mobile the unlock decision sits right after the headline numbers. */}
          {!accessGranted && (
            <div className="lg:hidden">
              <UnlockCard response={response} onSave={onSave} resumeHref={resumeHref} calculation={calculation} guestAccessToken={guestAccessToken} />
            </div>
          )}

          {s.vat.applicable && <VatCard vat={s.vat} />}

          {accessGranted && fullReport ? <PremiumReport report={fullReport} reportId={response.reportId} guestAccessToken={guestAccessToken} /> : <PremiumPreview response={response} />}

          <NextSteps steps={accessGranted && fullReport ? fullReport.nextSteps : s.nextSteps} />
          <AssumptionsCard response={response} />
        </div>

        <aside className="space-y-6 lg:sticky lg:top-24 lg:self-start">
          {!accessGranted && (
            <div className="hidden lg:block">
              <UnlockCard response={response} onSave={onSave} resumeHref={resumeHref} calculation={calculation} guestAccessToken={guestAccessToken} />
            </div>
          )}
          {accessGranted && <UnlockedCard reportId={response.reportId} via={response.unlockedVia} guestAccessToken={guestAccessToken} />}
          <AssistantPanel taxYear={s.taxYear} calculation={calculation} />
        </aside>
      </div>

    </div>
  );
}

function ResultHero({ response, createdAt, onEdit }: { response: TaxReportAccessResponse; createdAt?: string; onEdit?: () => void }) {
  const s = response.freeSummary;
  const isCompany = s.taxType === 'CompanyIncomeTax';
  return (
    <Card className="overflow-hidden">
      <div className="flex flex-col gap-6 p-5 sm:p-7 md:flex-row md:items-end md:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="neutral">Tax year {s.taxYear}</Badge>
            <Badge tone="neutral">{isCompany ? 'Company income tax' : `${taxpayerLabel(s.taxpayerType)} · personal income tax`}</Badge>
            {s.companyClassification && <Badge tone="brand">{s.companyClassification}</Badge>}
            {response.accessGranted ? <Badge tone="gold">Full report</Badge> : <Badge tone="neutral">Free summary</Badge>}
          </div>
          <p className="mt-5 text-sm font-medium text-ink-subtle">Estimated {isCompany ? 'company tax' : 'tax'} payable for the year</p>
          <p className="figure mt-1 text-[40px] font-semibold leading-none tracking-tight text-ink sm:text-[52px]" aria-live="polite">
            {formatCurrency(s.estimatedTaxPayable)}
          </p>
          <p className="mt-3 text-[15px] text-ink-muted">
            {s.estimatedMonthlyPaye != null ? (
              <>
                About <span className="figure font-semibold text-ink">{formatCurrency(s.estimatedMonthlyPaye)}</span> a month in PAYE on your salary.
              </>
            ) : s.estimatedTaxPayable > 0 ? (
              <>
                About <span className="figure font-semibold text-ink">{formatCurrency(s.estimatedTaxPayable / 12)}</span> a month if set aside evenly.
              </>
            ) : (
              'No tax is estimated on the figures you entered.'
            )}
          </p>
        </div>
        <div className="flex shrink-0 flex-col items-start gap-2 md:items-end">
          <div className="flex items-center gap-2 text-[13px] text-ink-subtle">
            <span>{s.ruleSet.name}</span>
            <VerificationBadge status={s.ruleSet.verificationStatus} />
          </div>
          {createdAt && <p className="text-[13px] text-ink-subtle">Calculated {formatDate(createdAt)}</p>}
          {onEdit && (
            <Button variant="secondary" size="sm" onClick={onEdit}>
              Edit answers
            </Button>
          )}
        </div>
      </div>
    </Card>
  );
}

function VatCard({ vat }: { vat: TaxReportAccessResponse['freeSummary']['vat'] }) {
  return (
    <Card className="p-5 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${vat.vatRequired ? 'bg-amber-50 text-amber-700' : 'bg-emerald-50 text-emerald-700'}`}>
            {vat.vatRequired ? <AlertTriangle /> : <CheckCircle />}
          </span>
          <div>
            <h2 className="text-base font-semibold">{vat.vatRequired ? 'VAT registration likely required' : 'Likely below the VAT threshold'}</h2>
            <p className="mt-1 text-sm leading-6 text-ink-muted">{vat.statusMessage}</p>
          </div>
        </div>
        <VerificationBadge status={vat.verificationStatus} />
      </div>
      <div className="mt-4">
        <div className="flex justify-between text-[13px] text-ink-subtle">
          <span>Turnover {formatCurrency(vat.annualTurnover)}</span>
          <span>Threshold {formatCurrency(vat.threshold)}</span>
        </div>
        <div className="mt-2 h-2 rounded-full bg-surface-muted" role="meter" aria-valuemin={0} aria-valuemax={vat.threshold} aria-valuenow={Math.min(vat.annualTurnover, vat.threshold)} aria-label="Turnover against VAT threshold">
          <div className={`h-2 rounded-full ${vat.vatRequired ? 'bg-amber-500' : 'bg-brand-600'}`} style={{ width: `${Math.min(100, (vat.annualTurnover / Math.max(vat.threshold, 1)) * 100)}%` }} />
        </div>
      </div>
    </Card>
  );
}

function Section({ title, description, children, action }: { title: string; description?: string; children: ReactNode; action?: ReactNode }) {
  return (
    <Card className="p-5 sm:p-6">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold">{title}</h2>
          {description && <p className="mt-0.5 text-sm text-ink-subtle">{description}</p>}
        </div>
        {action}
      </div>
      {children}
    </Card>
  );
}

function PremiumReport({ report, reportId, guestAccessToken }: { report: FullTaxReport; reportId?: string | null; guestAccessToken?: string }) {
  return (
    <>
      {report.companyTax ? (
        <Section title="Company tax computation" description={`${report.companyTax.classification} under ${report.ruleSet.name}`}>
          <dl className="divide-y divide-line text-sm">
            {[
              ['Turnover', formatCurrency(report.companyTax.turnover)],
              ['Allowable expenses', formatCurrency(report.companyTax.allowableExpenses)],
              ['Assessable profit', formatCurrency(report.companyTax.assessableProfit)],
              [`Company income tax @ ${formatPercent(report.companyTax.incomeTaxRate, 0)}`, formatCurrency(report.companyTax.incomeTax)],
              [`${report.companyTax.levyName} @ ${formatPercent(report.companyTax.levyRate, 0)}`, formatCurrency(report.companyTax.levy)],
            ].map(([label, value]) => (
              <div key={label} className="flex justify-between gap-4 py-2.5">
                <dt className="text-ink-muted">{label}</dt>
                <dd className="figure font-medium">{value}</dd>
              </div>
            ))}
          </dl>
        </Section>
      ) : (
        report.taxBandBreakdown.length > 0 && (
          <Section title="How each naira is taxed" description="Your taxable income split across the progressive bands.">
            <BandBreakdown bands={report.taxBandBreakdown} />
          </Section>
        )
      )}

      <Section title="Step-by-step calculation">
        <ol className="space-y-0 divide-y divide-line">
          {report.calculationSteps.map((step, index) => (
            <li key={step.label} className="flex gap-4 py-3">
              <span className="figure mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-surface-muted text-xs font-semibold text-ink-muted">{index + 1}</span>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap justify-between gap-x-4">
                  <p className="text-sm font-medium">{step.label}</p>
                  <p className="figure text-sm font-semibold">{formatCurrency(step.amount)}</p>
                </div>
                <p className="mt-0.5 text-[13px] leading-5 text-ink-subtle">{step.description}</p>
              </div>
            </li>
          ))}
        </ol>
      </Section>

      {report.deductionAnalysis.length > 0 && (
        <Section title={report.companyTax ? 'Expenses applied' : 'Deduction analysis'} description="What you claimed and what the rules allow.">
          <div className="-mx-1 overflow-x-auto">
            <table className="w-full min-w-[440px] text-sm">
              <thead>
                <tr className="text-left text-[13px] text-ink-subtle">
                  <th scope="col" className="px-1 pb-2 font-medium">Item</th>
                  <th scope="col" className="px-1 pb-2 text-right font-medium">Claimed</th>
                  <th scope="col" className="px-1 pb-2 text-right font-medium">Allowed</th>
                </tr>
              </thead>
              <tbody>
                {report.deductionAnalysis.map((line) => (
                  <tr key={line.label} className="border-t border-line align-top">
                    <th scope="row" className="px-1 py-2.5 text-left font-normal">
                      <span className="font-medium text-ink">{line.label}</span>
                      <span className="mt-0.5 block text-[13px] leading-5 text-ink-subtle">{line.note}</span>
                    </th>
                    <td className="figure px-1 py-2.5 text-right">{formatCurrency(line.claimed)}</td>
                    <td className={`figure px-1 py-2.5 text-right font-semibold ${line.allowed < line.claimed ? 'text-amber-700' : ''}`}>{formatCurrency(line.allowed)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Section>
      )}

      {report.optimizationTips.length > 0 && (
        <Section title="Recommendations" description="Legal ways to make sure you are not overpaying.">
          <ul className="space-y-3">
            {report.optimizationTips.map((tip) => (
              <li key={tip} className="flex gap-3 text-sm leading-6">
                <CheckCircle size={18} className="mt-0.5 shrink-0 text-brand-600" />
                <span>{tip}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {report.complianceWarnings.length > 0 && (
        <Section title="Compliance points">
          <ul className="space-y-3">
            {report.complianceWarnings.map((warning) => (
              <li key={warning} className="flex gap-3 text-sm leading-6">
                <AlertTriangle size={18} className="mt-0.5 shrink-0 text-amber-600" />
                <span>{warning}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {reportId && (
        <Card className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
          <div className="flex items-start gap-3">
            <FileText className="mt-0.5 shrink-0 text-ink-subtle" />
            <div>
              <h2 className="text-base font-semibold">PDF report</h2>
              <p className="text-sm text-ink-subtle">A formatted copy for your records or your accountant.</p>
            </div>
          </div>
          <PdfButton reportId={reportId} guestAccessToken={guestAccessToken} />
        </Card>
      )}
    </>
  );
}

export function PdfButton({ reportId, variant = 'secondary', guestAccessToken }: { reportId: string; variant?: 'primary' | 'secondary'; guestAccessToken?: string }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const download = async () => {
    setLoading(true);
    setError(null);
    try {
      const { blob, fileName } = guestAccessToken
        ? await apiService.downloadGuestReportPdf(reportId, guestAccessToken)
        : await apiService.downloadReportPdf(reportId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError((err as ApiError).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-start gap-1 sm:items-end">
      <Button variant={variant} onClick={download} loading={loading} loadingText="Preparing PDF…">
        <Download size={17} /> Download PDF
      </Button>
      {error && <p className="text-[13px] text-red-600" role="alert">{error}</p>}
    </div>
  );
}

/** Shows what the premium report contains — using placeholders, never the user's real premium data. */
function PremiumPreview({ response }: { response: TaxReportAccessResponse }) {
  const { freeSummary: s, premiumGate } = response;
  return (
    <Card className="overflow-hidden">
      <div className="border-b border-line p-5 sm:p-6">
        <div className="flex items-center gap-2">
          <Lock size={16} className="text-gold-600" />
          <p className="text-sm font-semibold">In your full report</p>
        </div>
        {s.teaserInsights.length > 0 && (
          <ul className="mt-3 space-y-2">
            {s.teaserInsights.map((insight) => (
              <li key={insight} className="flex gap-2.5 text-sm text-ink-muted">
                <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-gold-500" aria-hidden="true" />
                {insight}
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="relative p-5 sm:p-6" aria-hidden="true">
        <div className="space-y-3 blur-[3px]">
          {[72, 58, 84, 46, 64].map((width, index) => (
            <div key={index} className="flex items-center gap-4">
              <div className="h-3 w-28 rounded bg-line" />
              <div className="h-3 flex-1 rounded bg-surface-muted">
                <div className="h-3 rounded bg-brand-200" style={{ width: `${width}%` }} />
              </div>
              <div className="h-3 w-16 rounded bg-line" />
            </div>
          ))}
        </div>
        <div className="absolute inset-0 bg-gradient-to-b from-surface/10 to-surface/80" />
      </div>
      <ul className="grid gap-x-6 gap-y-2.5 border-t border-line p-5 sm:grid-cols-2 sm:p-6">
        {premiumGate.lockedFeatures.map((feature) => (
          <li key={feature} className="flex gap-2.5 text-sm">
            <Lock size={15} className="mt-0.5 shrink-0 text-ink-subtle" />
            {feature}
          </li>
        ))}
      </ul>
    </Card>
  );
}

// Payment status (enabled / test mode) is fetched once per page load.
let paymentsConfigRequest: Promise<PaymentsConfig> | null = null;

function usePaymentsConfig() {
  const [config, setConfig] = useState<PaymentsConfig | null>(null);
  useEffect(() => {
    paymentsConfigRequest ??= apiService.paymentsConfig().catch((error) => {
      paymentsConfigRequest = null;
      throw error;
    });
    paymentsConfigRequest.then(setConfig).catch(() => undefined);
  }, []);
  return config;
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface UnlockCardProps {
  response: TaxReportAccessResponse;
  onSave?: () => Promise<void>;
  resumeHref: string;
  calculation?: TaxCalculationRequest;
  guestAccessToken?: string;
}

function UnlockCard({ response, onSave, resumeHref, calculation, guestAccessToken }: UnlockCardProps) {
  const { isAuthenticated, user } = useAuth();
  const { state, startReport, startSubscription } = useCheckout();
  const config = usePaymentsConfig();
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  // The card renders twice (mobile and desktop layouts), so ids must be unique per instance.
  const emailId = useId();
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const gate = response.premiumGate;
  const reportId = response.reportId;
  const canBuy = !!reportId || !!calculation;
  const paymentsUnavailable = !gate.paymentsEnabled || state.status === 'unavailable';
  const starting = state.status === 'starting';

  const save = async () => {
    if (!onSave) return;
    setSaving(true);
    setSaveError(null);
    try {
      await onSave();
    } catch (err) {
      setSaveError((err as ApiError).message);
    } finally {
      setSaving(false);
    }
  };

  const buyAsGuest = (event: FormEvent) => {
    event.preventDefault();
    const value = email.trim();
    if (!EMAIL_PATTERN.test(value)) {
      setEmailError('Enter a valid email address for your receipt.');
      return;
    }
    setEmailError(null);
    startReport({ taxReportId: reportId, accessToken: guestAccessToken, email: value, calculation }, true);
  };

  return (
    <Card className="overflow-hidden">
      <div className="bg-night p-5 text-white sm:p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.08em] text-gold-300">Full tax report</p>
        <p className="mt-2 text-lg font-semibold leading-snug text-white">{gate.headline}</p>
        <p className="mt-1.5 text-sm leading-6 text-slate-300">{gate.message}</p>
        <p className="mt-4 flex items-baseline gap-1.5">
          <span className="figure text-3xl font-semibold">{formatCurrency(gate.paymentAmount)}</span>
          <span className="text-sm text-slate-400">one-time</span>
        </p>
      </div>
      <div className="space-y-3 p-5 sm:p-6">
        {config?.enabled && config.testMode && (
          <p className="rounded-lg bg-amber-50 px-3 py-2 text-[13px] leading-5 text-amber-900">
            <span className="font-semibold">Test mode:</span> use a Paystack test card. No real money is charged.
          </p>
        )}

        {state.status === 'unlocked' ? (
          <Alert tone="success">This report is already unlocked. Refresh the page to see it.</Alert>
        ) : paymentsUnavailable ? (
          <>
            <Button block disabled>
              <Lock size={16} /> Unlock — coming soon
            </Button>
            <Alert tone="info">
              {state.status === 'unavailable' ? state.message : 'Online payments are launching soon.'}
              {isAuthenticated && reportId && (
                <>
                  {' '}Your report is saved —{' '}
                  <Link className="link" href={`/dashboard/reports/${reportId}`}>
                    view it in your dashboard
                  </Link>
                  .
                </>
              )}
            </Alert>
          </>
        ) : !isAuthenticated ? (
          canBuy ? (
            <form onSubmit={buyAsGuest} noValidate className="space-y-3">
              <div>
                <label htmlFor={emailId} className="label">
                  Email for your receipt
                </label>
                <input
                  id={emailId}
                  type="email"
                  inputMode="email"
                  autoComplete="email"
                  className="input"
                  placeholder="you@example.com"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  aria-invalid={!!emailError || undefined}
                  aria-describedby={emailError ? `${emailId}-error` : undefined}
                />
                {emailError && (
                  <p id={`${emailId}-error`} className="error-text" role="alert">
                    {emailError}
                  </p>
                )}
              </div>
              <Button type="submit" block loading={starting} loadingText="Opening secure checkout…">
                Unlock for {formatCurrency(gate.paymentAmount)}
              </Button>
              <p className="text-center text-[13px] text-ink-subtle">
                No account needed.{' '}
                <Link className="link" href={`/auth/login?next=${encodeURIComponent(resumeHref)}`}>
                  Sign in
                </Link>{' '}
                to keep reports in a dashboard.
              </p>
            </form>
          ) : null
        ) : (
          <>
            {canBuy && (
              <Button block onClick={() => startReport({ taxReportId: reportId, calculation }, false)} loading={starting} loadingText="Opening secure checkout…">
                Unlock for {formatCurrency(gate.paymentAmount)}
              </Button>
            )}
            {!reportId && onSave && (
              <Button block variant="secondary" onClick={save} loading={saving} loadingText="Saving…" disabled={starting}>
                Save to my account (free)
              </Button>
            )}
            {user?.userType === 'SME' && (
              <Button block variant="secondary" onClick={startSubscription} disabled={starting}>
                Or SME plan · {formatCurrency(gate.subscriptionAmount)}/month
              </Button>
            )}
            {saveError && <p className="text-[13px] text-red-600" role="alert">{saveError}</p>}
          </>
        )}

        {state.status === 'error' && <p className="text-[13px] text-red-600" role="alert">{state.message}</p>}
        {!paymentsUnavailable && state.status !== 'unlocked' && (
          <p className="flex gap-2 text-[13px] leading-5 text-ink-subtle">
            <Shield size={16} className="mt-0.5 shrink-0" />
            Secure payment by Paystack. Your report unlocks only after we confirm the payment with Paystack.
          </p>
        )}
      </div>
    </Card>
  );
}

function UnlockedCard({ reportId, via, guestAccessToken }: { reportId?: string | null; via?: string | null; guestAccessToken?: string }) {
  return (
    <Card className="p-5 sm:p-6">
      <div className="flex items-center gap-2 text-brand-800">
        <CheckCircle size={18} />
        <p className="text-sm font-semibold">Full report unlocked</p>
      </div>
      <p className="mt-1.5 text-sm text-ink-subtle">{via === 'Subscription' ? 'Included in your SME plan.' : 'Purchased for this report.'}</p>
      {reportId && (
        <div className="mt-4">
          <PdfButton reportId={reportId} variant="primary" guestAccessToken={guestAccessToken} />
        </div>
      )}
    </Card>
  );
}

function NextSteps({ steps }: { steps: string[] }) {
  if (!steps.length) return null;
  return (
    <Section title="Recommended next steps">
      <ol className="space-y-3">
        {steps.map((step, index) => {
          const [title, ...rest] = step.split(': ');
          return (
            <li key={step} className="flex gap-3 text-sm leading-6">
              <span className="figure mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand-50 text-xs font-semibold text-brand-800">{index + 1}</span>
              <span>{rest.length ? <><span className="font-medium">{title}:</span> {rest.join(': ')}</> : step}</span>
            </li>
          );
        })}
      </ol>
    </Section>
  );
}

function AssumptionsCard({ response }: { response: TaxReportAccessResponse }) {
  const s = response.freeSummary;
  return (
    <Card className="p-5 sm:p-6">
      <details className="group">
        <summary className="flex cursor-pointer list-none items-center justify-between gap-3 rounded-lg">
          <span className="flex items-center gap-2">
            <Info size={18} className="text-ink-subtle" />
            <span className="text-base font-semibold">Assumptions & legal basis</span>
          </span>
          <span className="text-[13px] font-medium text-brand-700 group-open:hidden">Show</span>
          <span className="hidden text-[13px] font-medium text-brand-700 group-open:inline">Hide</span>
        </summary>
        <div className="mt-4 space-y-4 text-sm leading-6 text-ink-muted">
          <ul className="list-disc space-y-1.5 pl-5">
            {s.assumptions.map((assumption) => (
              <li key={assumption}>{assumption}</li>
            ))}
          </ul>
          <p>
            <span className="font-medium text-ink">Legal basis:</span> {s.ruleSet.legalBasis} Effective from {formatDate(s.ruleSet.effectiveFrom, { day: 'numeric', month: 'long', year: 'numeric' })}.
          </p>
          {s.ruleSet.sources.length > 0 && (
            <div>
              <p className="font-medium text-ink">Sources reviewed</p>
              <ul className="mt-1 space-y-1">
                {s.ruleSet.sources.map((source) => (
                  <li key={source.url || source.title}>
                    {source.url ? (
                      <a href={source.url} target="_blank" rel="noopener noreferrer" className="link">
                        {source.title}
                      </a>
                    ) : (
                      source.title
                    )}
                  </li>
                ))}
              </ul>
            </div>
          )}
          <p className="rounded-lg bg-surface-subtle px-3 py-2 text-[13px]">
            This is an estimate, not legal or tax advice. Confirm your position with the Nigeria Revenue Service, your State Internal Revenue Service or a qualified tax professional before filing.
          </p>
        </div>
      </details>
    </Card>
  );
}
