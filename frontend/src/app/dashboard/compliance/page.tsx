'use client';

import { useMemo, useState } from 'react';
import { MoneyInput, Toggle } from '@/components/ui/Form';
import { AlertTriangle, CheckCircle, Info } from '@/components/ui/Icons';
import { Badge, Card, CardHeader, PageHeader, Skeleton, VerificationBadge } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useLocalChecklist } from '@/hooks/useLocalChecklist';
import { useTaxRules } from '@/hooks/useTaxRules';
import { audiencesFor, upcomingDeadlines } from '@/lib/deadlines';
import { formatCurrency, formatDate, formatPercent } from '@/lib/formatters';

export default function CompliancePage() {
  const { user } = useAuth();
  const isSme = user?.userType === 'SME';
  const { rules } = useTaxRules();
  const [turnover, setTurnover] = useState(0);
  const [assets, setAssets] = useState(0);
  const [professional, setProfessional] = useState(false);
  const { checked, toggle } = useLocalChecklist('compliance', user?.id);

  const obligations = useMemo(() => {
    if (!rules || !user) return [];
    const audiences = audiencesFor(user.userType, { vatRegistered: isSme, selfEmployed: !isSme });
    const upcoming = upcomingDeadlines(rules.deadlines, audiences);
    // One row per obligation: its next due date.
    return rules.deadlines
      .filter((d) => d.appliesTo.some((a) => audiences.includes(a)))
      .map((d) => ({ deadline: d, next: upcoming.find((u) => u.deadline.id === d.id) }));
  }, [rules, user, isSme]);

  if (!rules) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64" />
      </div>
    );
  }

  const vat = rules.vat;
  const cit = rules.companyIncomeTax;
  const vatRequired = turnover > 0 && (vat.exemptAtThreshold ? turnover > vat.registrationThreshold : turnover >= vat.registrationThreshold);
  const isSmall =
    turnover > 0 &&
    turnover <= cit.smallCompanyTurnoverLimit &&
    (cit.smallCompanyFixedAssetLimit == null || assets <= cit.smallCompanyFixedAssetLimit) &&
    !(cit.smallCompanyExcludesProfessionalServices && professional);

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow={`Tax year ${rules.taxYear}`}
        title="Compliance"
        description={isSme ? 'Check your VAT and company tax position, and track what you need to file.' : 'Track the filings and payments that apply to you.'}
      />

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader title="VAT & small-company check" description="Quick check using the current thresholds." action={<VerificationBadge status={vat.verificationStatus} />} />
          <div className="space-y-5 p-5 sm:p-6">
            <MoneyInput label="Expected annual turnover" value={turnover} onChange={setTurnover} hint="Total sales for the year, before expenses." />
            {isSme && (
              <>
                <MoneyInput label="Fixed assets" optional value={assets} onChange={setAssets} />
                <Toggle label="Professional services firm" checked={professional} onChange={setProfessional} />
              </>
            )}
            {turnover > 0 && (
              <div className="space-y-3" aria-live="polite">
                <Outcome
                  ok={!vatRequired}
                  title={vatRequired ? 'Register for VAT' : 'Likely exempt from charging VAT'}
                  body={
                    vatRequired
                      ? `Above the ${formatCurrency(vat.registrationThreshold)} threshold: charge ${formatPercent(vat.rate, 1)} VAT and file monthly returns.`
                      : `Within the ${formatCurrency(vat.registrationThreshold)} small-business threshold.`
                  }
                />
                {isSme && (
                  <Outcome
                    ok={isSmall}
                    title={isSmall ? 'Small company — 0% company income tax' : `Company income tax at ${formatPercent(cit.standardRate, 0)} applies`}
                    body={
                      isSmall
                        ? 'You still need to register, keep records and file annual returns.'
                        : `Plus ${cit.levyName.toLowerCase()} of ${formatPercent(cit.levyRate, 0)} on assessable profit. Run a full calculation for the amount.`
                    }
                  />
                )}
              </div>
            )}
            {vat.note && (
              <p className="flex gap-2 text-[13px] leading-5 text-ink-subtle">
                <Info size={16} className="mt-0.5 shrink-0" />
                {vat.note}
              </p>
            )}
          </div>
        </Card>

        <Card>
          <CardHeader title="Your obligations" description="Mark items done for the current period. Saved on this device." />
          <ul className="divide-y divide-line">
            {obligations.map(({ deadline, next }) => {
              const key = `${deadline.id}:${next?.period ?? ''}`;
              const done = !!checked[key];
              return (
                <li key={deadline.id} className="flex items-start gap-3 px-5 py-4 sm:px-6">
                  <input
                    id={`ob-${deadline.id}`}
                    type="checkbox"
                    checked={done}
                    onChange={() => toggle(key)}
                    className="mt-1 h-5 w-5 shrink-0 rounded border-line-strong text-brand-700 accent-brand-700"
                  />
                  <label htmlFor={`ob-${deadline.id}`} className="min-w-0 flex-1 cursor-pointer">
                    <span className={`block text-sm font-medium ${done ? 'text-ink-subtle line-through' : ''}`}>{deadline.title}</span>
                    <span className="mt-0.5 block text-[13px] leading-5 text-ink-subtle">{deadline.description}</span>
                    <span className="mt-1.5 flex flex-wrap items-center gap-2">
                      {next && <Badge tone={next.daysAway <= 7 && !done ? 'warning' : 'neutral'}>Next due {formatDate(next.date)} · {next.period}</Badge>}
                      <Badge tone="neutral">{deadline.authority}</Badge>
                    </span>
                  </label>
                </li>
              );
            })}
          </ul>
        </Card>
      </div>
    </div>
  );
}

function Outcome({ ok, title, body }: { ok: boolean; title: string; body: string }) {
  return (
    <div className={`flex gap-3 rounded-xl border p-4 ${ok ? 'border-emerald-200 bg-emerald-50' : 'border-amber-200 bg-amber-50'}`}>
      {ok ? <CheckCircle size={20} className="mt-0.5 shrink-0 text-emerald-700" /> : <AlertTriangle size={20} className="mt-0.5 shrink-0 text-amber-700" />}
      <div>
        <p className="text-sm font-semibold text-ink">{title}</p>
        <p className="mt-0.5 text-[13px] leading-5 text-ink-muted">{body}</p>
      </div>
    </div>
  );
}
