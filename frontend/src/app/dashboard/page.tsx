'use client';

import Link from 'next/link';
import { useMemo } from 'react';
import ReportsList from '@/components/dashboard/ReportsList';
import { ButtonLink } from '@/components/ui/Button';
import { AlertTriangle, ArrowRight, Calculator, Calendar, CheckCircle, ClipboardCheck, FileText, MessageCircle, Scale } from '@/components/ui/Icons';
import { Alert, Badge, Card, CardHeader, EmptyState, PageHeader, Skeleton, Stat } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useApi } from '@/hooks/useApi';
import { useLocalChecklist } from '@/hooks/useLocalChecklist';
import { useTaxRules } from '@/hooks/useTaxRules';
import { individualDocuments, smeDocuments } from '@/lib/checklists';
import { audiencesFor, upcomingDeadlines } from '@/lib/deadlines';
import { formatCurrency, formatDate, formatPercent } from '@/lib/formatters';
import { apiService } from '@/services/apiService';

function greeting() {
  const hour = new Date().getHours();
  return hour < 12 ? 'Good morning' : hour < 17 ? 'Good afternoon' : 'Good evening';
}

export default function DashboardOverview() {
  const { user } = useAuth();
  const isSme = user?.userType === 'SME';
  const { rules } = useTaxRules();
  const { data, loading, error, reload } = useApi(async () => {
    const [reports, entitlements] = await Promise.all([apiService.reports(), apiService.entitlements()]);
    const relevant = reports.filter((r) => (isSme ? r.reportType === 'SME' : r.reportType !== 'SME'));
    const latest = relevant[0] ?? reports[0];
    const latestDetail = latest ? await apiService.report(latest.id).catch(() => null) : null;
    return { reports, entitlements, latest, latestDetail };
  }, [isSme]);

  const { checked } = useLocalChecklist('documents', user?.id);
  const docs = isSme ? smeDocuments : individualDocuments;
  const docsDone = docs.filter((d) => checked[d.id]).length;

  const summary = data?.latestDetail?.report.freeSummary;
  const deadlines = useMemo(() => {
    if (!rules || !user) return [];
    const audiences = audiencesFor(user.userType, {
      selfEmployed: summary?.taxpayerType === 'SelfEmployed',
      vatRegistered: summary?.vat.vatRequired,
      employee: summary ? summary.taxpayerType !== 'SME' : true,
    });
    return upcomingDeadlines(rules.deadlines, audiences).slice(0, 4);
  }, [rules, user, summary]);

  if (!user) return null;

  return (
    <div className="space-y-8">
      <PageHeader
        eyebrow={isSme ? 'Business overview' : 'Personal overview'}
        title={`${greeting()}, ${user.firstName || 'there'}`}
        description={isSme ? 'Your business tax position, obligations and upcoming deadlines.' : 'Your tax position, saved reports and what is coming up.'}
        actions={
          <ButtonLink href={isSme ? '/calculate?type=sme' : '/calculate?type=individual'}>
            <Calculator size={17} /> New calculation
          </ButtonLink>
        }
      />

      {error && (
        <Alert tone="error" title="We couldn't load your dashboard">
          {error}{' '}
          <button type="button" className="link" onClick={reload}>
            Try again
          </button>
        </Alert>
      )}

      {loading ? (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-[92px]" />
          ))}
        </div>
      ) : data && data.reports.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Calculator />}
            title={isSme ? 'Run your first business tax check' : 'Run your first calculation'}
            description={isSme ? 'Find out your company tax position, VAT status and what to file — in about three minutes.' : 'See your estimated tax, effective rate and the deductions you can claim — in about three minutes.'}
            action={<ButtonLink href={isSme ? '/calculate?type=sme' : '/calculate'}>Start now</ButtonLink>}
          />
        </Card>
      ) : (
        summary && (
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Stat label={`Estimated tax · ${summary.taxYear}`} value={formatCurrency(summary.estimatedTaxPayable)} emphasis />
            {isSme ? (
              <>
                <Stat label="Company status" value={<span className="text-base">{summary.companyClassification ?? 'Sole proprietor'}</span>} />
                <Stat label="VAT position" value={<span className="text-base">{summary.vat.vatRequired ? 'Register for VAT' : 'Below threshold'}</span>} sub={`Threshold ${formatCurrency(summary.vat.threshold)}`} />
              </>
            ) : (
              <>
                <Stat label="Effective rate" value={formatPercent(summary.effectiveTaxRate, 1)} />
                <Stat label="Monthly PAYE" value={summary.estimatedMonthlyPaye != null ? formatCurrency(summary.estimatedMonthlyPaye) : '—'} />
              </>
            )}
            <Stat label="Documents ready" value={`${docsDone}/${docs.length}`} sub={<Link href="/dashboard/documents" className="link">Open checklist</Link>} />
          </div>
        )
      )}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="min-w-0 space-y-6">
          {isSme && summary && <ComplianceSnapshot vatRequired={summary.vat.vatRequired} classification={summary.companyClassification} flags={summary.complianceFlagCount} hasSubscription={!!data?.entitlements.hasActiveSubscription} />}

          <Card>
            <CardHeader
              title="Recent reports"
              action={
                data && data.reports.length > 0 ? (
                  <Link href="/dashboard/reports" className="text-sm font-medium text-brand-700 hover:underline">
                    View all
                  </Link>
                ) : undefined
              }
            />
            {loading ? (
              <div className="space-y-3 p-6">
                <Skeleton className="h-12" />
                <Skeleton className="h-12" />
              </div>
            ) : data && data.reports.length > 0 ? (
              <ReportsList reports={data.reports.slice(0, 4)} hasSubscription={data.entitlements.hasActiveSubscription} />
            ) : (
              <EmptyState icon={<FileText />} title="No saved reports" description="Reports you save from the calculator appear here." />
            )}
          </Card>
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader title="Coming up" action={<Link href="/dashboard/calendar" className="text-sm font-medium text-brand-700 hover:underline">Calendar</Link>} />
            {!rules ? (
              <div className="space-y-3 p-5">
                <Skeleton className="h-10" />
                <Skeleton className="h-10" />
              </div>
            ) : deadlines.length === 0 ? (
              <EmptyState icon={<Calendar />} title="Nothing due" description="No deadlines in the next 12 months for your profile." />
            ) : (
              <ul className="divide-y divide-line">
                {deadlines.map((item) => (
                  <li key={`${item.deadline.id}-${item.date.toISOString()}`} className="flex items-center gap-3 px-5 py-3.5">
                    <div className="flex w-11 shrink-0 flex-col items-center rounded-lg border border-line py-1">
                      <span className="text-[10px] font-semibold uppercase text-ink-subtle">{formatDate(item.date, { month: 'short' })}</span>
                      <span className="figure text-base font-semibold leading-none">{item.date.getDate()}</span>
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{item.deadline.title}</p>
                      <p className="text-xs text-ink-subtle">{item.period}</p>
                    </div>
                    <Badge tone={item.daysAway <= 7 ? 'warning' : 'neutral'}>{item.daysAway === 0 ? 'Today' : `${item.daysAway}d`}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </Card>

          <Card className="p-5">
            <div className="flex items-start gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
                <MessageCircle />
              </span>
              <div>
                <p className="text-sm font-semibold">Have a tax question?</p>
                <p className="mt-0.5 text-sm text-ink-subtle">Get plain-English answers grounded in the current rules.</p>
                <Link href="/dashboard/assistant" className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-brand-700 hover:underline">
                  Ask TaxNavi <ArrowRight size={14} />
                </Link>
              </div>
            </div>
          </Card>

          {data && !data.entitlements.hasActiveSubscription && isSme && (
            <Card className="p-5">
              <p className="text-sm font-semibold">SME plan</p>
              <p className="mt-1 text-sm text-ink-subtle">Unlimited full business reports, compliance tracking and saved history.</p>
              <ButtonLink href="/dashboard/billing" variant="secondary" size="sm" className="mt-3">
                See plan
              </ButtonLink>
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

function ComplianceSnapshot({ vatRequired, classification, flags, hasSubscription }: { vatRequired: boolean; classification?: string | null; flags: number; hasSubscription: boolean }) {
  const items = [
    {
      ok: !vatRequired,
      title: vatRequired ? 'VAT registration likely required' : 'Below VAT threshold',
      body: vatRequired ? 'Register, charge VAT on invoices and file monthly returns.' : 'Keep monitoring turnover each month.',
    },
    {
      ok: classification === 'Small company',
      title: classification ? classification : 'Business income taxed personally',
      body: classification === 'Small company' ? '0% company income tax — you must still file annual returns.' : classification ? 'Company income tax and levy apply on profit.' : 'Profits are taxed through your personal return.',
    },
  ];

  return (
    <Card>
      <CardHeader title="Compliance status" description="Based on your latest business report." action={<Link href="/dashboard/compliance" className="text-sm font-medium text-brand-700 hover:underline">Details</Link>} />
      <ul className="divide-y divide-line">
        {items.map((item) => (
          <li key={item.title} className="flex gap-3 px-5 py-4 sm:px-6">
            {item.ok ? <CheckCircle size={20} className="mt-0.5 shrink-0 text-brand-600" /> : <AlertTriangle size={20} className="mt-0.5 shrink-0 text-amber-600" />}
            <div>
              <p className="text-sm font-medium">{item.title}</p>
              <p className="text-[13px] text-ink-subtle">{item.body}</p>
            </div>
          </li>
        ))}
        <li className="flex gap-3 px-5 py-4 sm:px-6">
          <Scale size={20} className="mt-0.5 shrink-0 text-ink-subtle" />
          <div>
            <p className="text-sm font-medium">{flags} compliance {flags === 1 ? 'point' : 'points'} in your latest report</p>
            <p className="text-[13px] text-ink-subtle">{hasSubscription ? 'Open the report to review them.' : 'Unlock the full report or SME plan to see them.'}</p>
          </div>
        </li>
        <li className="flex gap-3 px-5 py-4 sm:px-6">
          <ClipboardCheck size={20} className="mt-0.5 shrink-0 text-ink-subtle" />
          <div>
            <p className="text-sm font-medium">Keep records for six years</p>
            <p className="text-[13px] text-ink-subtle">Invoices, receipts, payroll and bank statements.</p>
          </div>
        </li>
      </ul>
    </Card>
  );
}
