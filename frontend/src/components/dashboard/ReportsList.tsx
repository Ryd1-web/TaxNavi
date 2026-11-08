import Link from 'next/link';
import { formatCurrency, formatDate, taxpayerLabel } from '@/lib/formatters';
import { TaxReportListItem } from '@/types';
import { ChevronRight, Lock } from '@/components/ui/Icons';
import { Badge } from '@/components/ui/Primitives';

export default function ReportsList({ reports, hasSubscription }: { reports: TaxReportListItem[]; hasSubscription?: boolean }) {
  return (
    <ul className="divide-y divide-line">
      {reports.map((report) => {
        const unlocked = report.isUnlocked || hasSubscription;
        return (
          <li key={report.id}>
            <Link href={`/dashboard/reports/${report.id}`} className="flex items-center gap-4 px-5 py-4 transition-colors hover:bg-surface-subtle sm:px-6">
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-semibold">Tax year {report.taxYear}</p>
                  <Badge tone="neutral">{taxpayerLabel(report.reportType)}</Badge>
                  {unlocked ? <Badge tone="gold">Full report</Badge> : <Badge tone="neutral"><Lock size={11} /> Summary</Badge>}
                </div>
                <p className="mt-1 text-[13px] text-ink-subtle">
                  Income {formatCurrency(report.totalIncome)} · Saved {formatDate(report.createdAt)}
                </p>
              </div>
              <div className="text-right">
                <p className="figure text-sm font-semibold sm:text-base">{formatCurrency(report.taxLiability)}</p>
                <p className="text-xs text-ink-subtle">est. tax</p>
              </div>
              <ChevronRight size={18} className="shrink-0 text-ink-subtle" />
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
