'use client';

import { useParams, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import PublicLayout from '@/components/layout/PublicLayout';
import TaxResults from '@/components/tax/TaxResults';
import { Button, ButtonLink } from '@/components/ui/Button';
import { FileText } from '@/components/ui/Icons';
import { Alert, Card, EmptyState, Skeleton } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { getGuestReport, removeGuestReport } from '@/lib/guestReports';
import { apiService, ApiError } from '@/services/apiService';
import { TaxReportAccessResponse } from '@/types';

/** A report bought without an account. Access is proven by the token stored on this device. */
export default function GuestReportPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { isAuthenticated } = useAuth();
  const [token, setToken] = useState<string | null>(null);
  const [data, setData] = useState<{ createdAt: string; report: TaxReportAccessResponse } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [claiming, setClaiming] = useState(false);

  useEffect(() => {
    const stored = getGuestReport(id);
    if (!stored) {
      setError('This report is not available on this device.');
      return;
    }
    setToken(stored.accessToken);
    apiService
      .guestReport(id, stored.accessToken)
      .then(setData)
      .catch((err: ApiError) => setError(err.status === 404 ? 'This report is not available on this device.' : err.message));
  }, [id]);

  const claim = async () => {
    if (!token) return;
    setClaiming(true);
    try {
      await apiService.claimReport(id, token);
      removeGuestReport(id);
      router.replace(`/dashboard/reports/${id}`);
    } catch (err) {
      setError((err as ApiError).message);
      setClaiming(false);
    }
  };

  return (
    <PublicLayout>
      <div className="container-page py-8 sm:py-10">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="eyebrow">Your report</p>
            <h1 className="mt-1 text-2xl font-semibold sm:text-[28px]">Here&apos;s your tax position</h1>
          </div>
          {data && isAuthenticated && (
            <Button variant="secondary" size="sm" onClick={claim} loading={claiming} loadingText="Saving…">
              Save to my account
            </Button>
          )}
        </div>

        {!isAuthenticated && data?.report.accessGranted && (
          <Alert tone="info" className="mb-6">
            This report is saved on this device only.{' '}
            <a className="link" href={`/auth/register?next=${encodeURIComponent(`/report/${id}`)}`}>
              Create a free account
            </a>{' '}
            to keep it safe and open it anywhere.
          </Alert>
        )}

        {error ? (
          <Card>
            <EmptyState
              icon={<FileText />}
              title="Report not available"
              description={`${error} Reports bought without an account can only be opened on the device used to buy them.`}
              action={<ButtonLink href="/calculate">Start a new calculation</ButtonLink>}
            />
          </Card>
        ) : !data || !token ? (
          <div className="space-y-4" aria-busy="true">
            <Skeleton className="h-48" />
            <Skeleton className="h-64" />
          </div>
        ) : (
          <TaxResults response={data.report} createdAt={data.createdAt} guestAccessToken={token} resumeHref={`/report/${id}`} />
        )}
      </div>
    </PublicLayout>
  );
}
