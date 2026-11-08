'use client';

import { useState } from 'react';
import ReportsList from '@/components/dashboard/ReportsList';
import { ButtonLink } from '@/components/ui/Button';
import { Calculator, FileText } from '@/components/ui/Icons';
import { Alert, Card, EmptyState, PageHeader, Skeleton } from '@/components/ui/Primitives';
import { useApi } from '@/hooks/useApi';
import { apiService } from '@/services/apiService';

export default function ReportsPage() {
  const { data, loading, error, reload } = useApi(async () => {
    const [reports, entitlements] = await Promise.all([apiService.reports(), apiService.entitlements()]);
    return { reports, entitlements };
  });
  const [year, setYear] = useState<number | 'all'>('all');

  const years = Array.from(new Set(data?.reports.map((r) => r.taxYear) ?? [])).sort((a, b) => b - a);
  const visible = data?.reports.filter((r) => year === 'all' || r.taxYear === year) ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Tax history"
        title="Reports"
        description="Every calculation you have saved, newest first."
        actions={
          <ButtonLink href="/calculate">
            <Calculator size={17} /> New calculation
          </ButtonLink>
        }
      />

      {error && (
        <Alert tone="error">
          {error}{' '}
          <button type="button" className="link" onClick={reload}>
            Try again
          </button>
        </Alert>
      )}

      {years.length > 1 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by tax year">
          {(['all', ...years] as const).map((value) => (
            <button
              key={value}
              type="button"
              aria-pressed={year === value}
              onClick={() => setYear(value)}
              className={`min-h-[36px] rounded-full border px-3.5 text-[13px] font-medium ${year === value ? 'border-ink bg-ink text-surface' : 'border-line-strong bg-surface text-ink-muted hover:text-ink'}`}
            >
              {value === 'all' ? 'All years' : value}
            </button>
          ))}
        </div>
      )}

      <Card>
        {loading ? (
          <div className="space-y-3 p-6">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} className="h-14" />
            ))}
          </div>
        ) : visible.length > 0 ? (
          <ReportsList reports={visible} hasSubscription={data?.entitlements.hasActiveSubscription} />
        ) : (
          <EmptyState
            icon={<FileText />}
            title="No reports yet"
            description="Run a calculation and choose “Save to my account” to keep it here."
            action={<ButtonLink href="/calculate">Calculate my tax</ButtonLink>}
          />
        )}
      </Card>
    </div>
  );
}
