'use client';

import { useState } from 'react';
import { formatCurrency, formatCurrencyCompact, formatPercent } from '@/lib/formatters';

interface Segment {
  key: string;
  label: string;
  description: string;
  value: number;
  color: string;
}

interface Props {
  grossIncome: number;
  taxableIncome: number;
  taxPayable: number;
  isCompany?: boolean;
}

/**
 * Part-to-whole: how gross income splits into untaxed, taxed-and-kept, and tax.
 * Colours come from the per-theme --chart-* variables, each validated with the dataviz checker
 * (light CVD ΔE 7.5 → legend + direct labels + gaps carried as secondary encoding).
 */
export default function IncomeBreakdownBar({ grossIncome, taxableIncome, taxPayable, isCompany }: Props) {
  const [active, setActive] = useState<string | null>(null);
  const [showTable, setShowTable] = useState(false);

  if (grossIncome <= 0) return null;

  const sheltered = Math.max(0, grossIncome - taxableIncome);
  const kept = Math.max(0, taxableIncome - taxPayable);
  const segments: Segment[] = [
    {
      key: 'sheltered',
      label: isCompany ? 'Covered by expenses' : 'Not taxed',
      description: isCompany ? 'Allowable business expenses' : 'Reliefs, deductions and expenses',
      value: sheltered,
      color: 'rgb(var(--chart-sheltered))',
    },
    { key: 'kept', label: 'Taxed, yours to keep', description: 'Taxable income after tax', value: kept, color: 'rgb(var(--chart-kept))' },
    { key: 'tax', label: 'Estimated tax', description: isCompany ? 'Company tax and levy' : 'Personal income tax', value: taxPayable, color: 'rgb(var(--chart-tax))' },
  ].filter((segment) => segment.value > 0);

  const activeSegment = segments.find((segment) => segment.key === active);

  return (
    <figure className="m-0">
      <figcaption className="flex flex-wrap items-baseline justify-between gap-2">
        <span className="text-sm font-semibold text-ink">Where your {isCompany ? 'turnover' : 'income'} goes</span>
        <button type="button" className="text-[13px] font-medium text-brand-700 hover:underline" onClick={() => setShowTable((v) => !v)} aria-pressed={showTable}>
          {showTable ? 'Show chart' : 'View as table'}
        </button>
      </figcaption>

      {showTable ? (
        <table className="mt-3 w-full text-sm">
          <caption className="sr-only">Breakdown of {formatCurrency(grossIncome)}</caption>
          <thead>
            <tr className="text-left text-[13px] text-ink-subtle">
              <th scope="col" className="py-2 font-medium">Portion</th>
              <th scope="col" className="py-2 text-right font-medium">Amount</th>
              <th scope="col" className="py-2 text-right font-medium">Share</th>
            </tr>
          </thead>
          <tbody>
            {segments.map((segment) => (
              <tr key={segment.key} className="border-t border-line">
                <th scope="row" className="py-2 text-left font-normal">{segment.label}</th>
                <td className="figure py-2 text-right">{formatCurrency(segment.value)}</td>
                <td className="figure py-2 text-right text-ink-subtle">{formatPercent(segment.value / grossIncome, 1)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <div className="relative mt-4">
          {activeSegment && (
            <div role="tooltip" className="pointer-events-none absolute -top-2 left-1/2 z-10 -translate-x-1/2 -translate-y-full whitespace-nowrap rounded-lg bg-night px-3 py-2 text-xs text-white shadow-raised">
              <span className="font-semibold">{activeSegment.label}</span> · <span className="figure">{formatCurrency(activeSegment.value)}</span>{' '}
              <span className="text-slate-300">({formatPercent(activeSegment.value / grossIncome, 1)})</span>
            </div>
          )}
          <div className="flex h-10 w-full gap-[2px] overflow-hidden rounded-[4px]" role="list" aria-label={`Breakdown of ${formatCurrency(grossIncome)}`}>
            {segments.map((segment) => {
              const share = segment.value / grossIncome;
              return (
                <div
                  key={segment.key}
                  role="listitem"
                  tabIndex={0}
                  aria-label={`${segment.label}: ${formatCurrency(segment.value)}, ${formatPercent(share, 1)}`}
                  onMouseEnter={() => setActive(segment.key)}
                  onMouseLeave={() => setActive(null)}
                  onFocus={() => setActive(segment.key)}
                  onBlur={() => setActive(null)}
                  className="flex min-w-[3px] items-center justify-center text-xs font-semibold text-white outline-none dark:text-[#0b1220] transition-opacity focus-visible:ring-2 focus-visible:ring-ink focus-visible:ring-offset-2"
                  style={{ width: `${share * 100}%`, background: segment.color, opacity: active && active !== segment.key ? 0.55 : 1 }}
                >
                  {/* Direct label only where it fits comfortably; dark text on the lighter dark-theme fills keeps AA contrast. */}
                  {share >= 0.16 && <span className="figure px-1">{formatPercent(share, 0)}</span>}
                </div>
              );
            })}
          </div>
          <ul className="mt-4 grid gap-3 sm:grid-cols-3">
            {segments.map((segment) => (
              <li key={segment.key} className="flex items-start gap-2.5">
                <span className="mt-1.5 h-2.5 w-2.5 shrink-0 rounded-sm" style={{ background: segment.color }} aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-[13px] text-ink-subtle">{segment.label}</p>
                  <p className="figure text-sm font-semibold text-ink">
                    {formatCurrencyCompact(segment.value)} <span className="font-normal text-ink-subtle">· {formatPercent(segment.value / grossIncome, 0)}</span>
                  </p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </figure>
  );
}
