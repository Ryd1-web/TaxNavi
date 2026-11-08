'use client';

import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useState } from 'react';
import TaxResults from '@/components/tax/TaxResults';
import { Button } from '@/components/ui/Button';
import { ArrowLeft, FileText, Trash } from '@/components/ui/Icons';
import { Alert, Card, EmptyState, Skeleton } from '@/components/ui/Primitives';
import { useApi } from '@/hooks/useApi';
import { apiService, ApiError } from '@/services/apiService';

export default function ReportDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data, loading, error } = useApi(() => apiService.report(id), [id]);
  const [confirming, setConfirming] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const remove = async () => {
    setDeleting(true);
    setDeleteError(null);
    try {
      await apiService.deleteReport(id);
      router.replace('/dashboard/reports');
    } catch (err) {
      setDeleteError((err as ApiError).message);
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link href="/dashboard/reports" className="inline-flex items-center gap-1.5 text-sm font-medium text-ink-muted hover:text-ink">
          <ArrowLeft size={16} /> All reports
        </Link>
        {data && (
          <div className="flex items-center gap-2">
            {confirming ? (
              <>
                <span className="text-sm text-ink-muted">Delete this report?</span>
                <Button variant="danger" size="sm" onClick={remove} loading={deleting}>
                  Delete
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setConfirming(false)}>
                  Cancel
                </Button>
              </>
            ) : (
              <Button variant="ghost" size="sm" onClick={() => setConfirming(true)}>
                <Trash size={16} /> Delete
              </Button>
            )}
          </div>
        )}
      </div>

      {deleteError && <Alert tone="error">{deleteError}</Alert>}

      {loading ? (
        <div className="space-y-4">
          <Skeleton className="h-48" />
          <Skeleton className="h-64" />
        </div>
      ) : error || !data ? (
        <Card>
          <EmptyState icon={<FileText />} title="Report not available" description={error ?? 'This report may have been deleted.'} />
        </Card>
      ) : (
        <TaxResults response={data.report} createdAt={data.createdAt} resumeHref={`/dashboard/reports/${id}`} />
      )}
    </div>
  );
}
