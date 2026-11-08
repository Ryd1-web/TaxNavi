import { Skeleton } from '@/components/ui/Primitives';

/** Shown instantly while a report route loads, so navigation never feels stuck. */
export default function ReportLoading() {
  return (
    <div className="space-y-4" aria-busy="true">
      <Skeleton className="h-5 w-28" />
      <Skeleton className="h-48" />
      <Skeleton className="h-64" />
      <span className="sr-only">Loading report…</span>
    </div>
  );
}
