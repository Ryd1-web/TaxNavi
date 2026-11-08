'use client';

import { useMemo, useState } from 'react';
import { Calendar } from '@/components/ui/Icons';
import { Badge, Card, EmptyState, PageHeader, Skeleton, VerificationBadge } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useTaxRules } from '@/hooks/useTaxRules';
import { upcomingDeadlines } from '@/lib/deadlines';
import { formatDate } from '@/lib/formatters';

const AUDIENCES = [
  { id: 'Employee', label: 'Employee' },
  { id: 'SelfEmployed', label: 'Self-employed' },
  { id: 'Employer', label: 'Employer' },
  { id: 'Company', label: 'Company' },
  { id: 'VatRegistered', label: 'VAT-registered' },
];

export default function CalendarPage() {
  const { user } = useAuth();
  const { rules } = useTaxRules();
  const [selected, setSelected] = useState<string[]>(() =>
    user?.userType === 'SME' ? ['Company', 'Employer', 'VatRegistered'] : ['Employee', 'SelfEmployed'],
  );

  const grouped = useMemo(() => {
    if (!rules) return [];
    const items = upcomingDeadlines(rules.deadlines, selected.length ? selected : ['__none__']);
    const groups = new Map<string, typeof items>();
    items.forEach((item) => {
      const key = formatDate(item.date, { month: 'long', year: 'numeric' });
      groups.set(key, [...(groups.get(key) ?? []), item]);
    });
    return Array.from(groups.entries());
  }, [rules, selected]);

  const toggle = (id: string) => setSelected((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]));

  return (
    <div className="space-y-6">
      <PageHeader eyebrow="Next 12 months" title="Tax calendar" description="Deadlines that apply to you. If a date falls on a weekend or public holiday, confirm it with your tax authority." />

      <div className="flex flex-wrap gap-2" role="group" aria-label="Show deadlines for">
        {AUDIENCES.map((audience) => {
          const on = selected.includes(audience.id);
          return (
            <button
              key={audience.id}
              type="button"
              aria-pressed={on}
              onClick={() => toggle(audience.id)}
              className={`min-h-[36px] rounded-full border px-3.5 text-[13px] font-medium ${on ? 'border-ink bg-ink text-surface' : 'border-line-strong bg-surface text-ink-muted hover:text-ink'}`}
            >
              {audience.label}
            </button>
          );
        })}
      </div>

      {!rules ? (
        <Skeleton className="h-80" />
      ) : grouped.length === 0 ? (
        <Card>
          <EmptyState icon={<Calendar />} title="No deadlines" description="Select at least one profile above to see its deadlines." />
        </Card>
      ) : (
        <div className="space-y-6">
          {grouped.map(([month, items]) => (
            <section key={month} aria-labelledby={`m-${month}`}>
              <h2 id={`m-${month}`} className="mb-2 text-sm font-semibold text-ink-subtle">
                {month}
              </h2>
              <Card>
                <ul className="divide-y divide-line">
                  {items.map((item) => (
                    <li key={`${item.deadline.id}-${item.date.toISOString()}`} className="flex items-start gap-4 px-5 py-4">
                      <div className="flex w-12 shrink-0 flex-col items-center rounded-lg border border-line py-1.5">
                        <span className="text-[10px] font-semibold uppercase text-ink-subtle">{formatDate(item.date, { weekday: 'short' })}</span>
                        <span className="figure text-lg font-semibold leading-none">{item.date.getDate()}</span>
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="text-sm font-semibold">{item.deadline.title}</p>
                          {item.daysAway <= 7 && <Badge tone="warning">{item.daysAway === 0 ? 'Today' : `In ${item.daysAway} days`}</Badge>}
                        </div>
                        <p className="mt-0.5 text-[13px] leading-5 text-ink-subtle">
                          {item.deadline.description} <span className="text-ink-muted">Covers {item.period}.</span>
                        </p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          <Badge tone="neutral">{item.deadline.authority}</Badge>
                          <VerificationBadge status={item.deadline.verificationStatus} />
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              </Card>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}
