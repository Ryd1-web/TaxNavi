'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { apiService } from '@/services/apiService';
import Navigation from '@/components/Navigation';
import { TaxReport } from '@/types';

export default function ReportsPage() {
  const router = useRouter();
  const { isAuthenticated, user, logout } = useAuth();
  const [reports, setReports] = useState<TaxReport[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/auth/login');
      return;
    }

    const fetchReports = async () => {
      try {
        const response = await apiService.getReports();
        setReports(response.data.data || []);
      } catch (error) {
        console.error('Failed to fetch reports:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchReports();
  }, [isAuthenticated, router]);

  if (!isAuthenticated) return null;

  return (
    <div>
      <Navigation user={user} onLogout={logout} />

      <div className="container py-8">
        <h1 className="text-4xl font-bold mb-8">Tax Reports</h1>

        {isLoading ? (
          <div className="text-center py-12">
            <p className="text-gray-600">Loading reports...</p>
          </div>
        ) : reports.length === 0 ? (
          <div className="card text-center py-12">
            <p className="text-gray-600 mb-4">No reports yet</p>
            <Link href="/tax/individual" className="btn btn-primary">
              Create First Assessment
            </Link>
          </div>
        ) : (
          <div className="grid gap-6">
            {reports.map((report) => (
              <div key={report.id} className="card">
                <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-6">
                  <div>
                    <p className="text-gray-600 text-sm">Report Type</p>
                    <p className="text-xl font-semibold">{report.reportType}</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-sm">Total Income</p>
                    <p className="text-xl font-semibold">₦{report.totalIncome.toLocaleString()}</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-sm">Tax Liability</p>
                    <p className="text-xl font-semibold text-red-600">₦{report.taxLiability.toLocaleString()}</p>
                  </div>
                  <div>
                    <p className="text-gray-600 text-sm">Projected Savings</p>
                    <p className="text-xl font-semibold text-green-600">₦{report.projectedSavings.toLocaleString()}</p>
                  </div>
                </div>

                <div className="flex justify-between items-center pt-6 border-t">
                  <p className="text-sm text-gray-600">
                    {new Date(report.createdAt).toLocaleString()}
                  </p>
                  <div className="flex gap-4">
                    <Link href={`/reports/${report.id}`} className="btn btn-primary">
                      View Details
                    </Link>
                    {report.pdfUrl && (
                      <a href={report.pdfUrl} target="_blank" rel="noopener noreferrer" className="btn btn-secondary">
                        Download PDF
                      </a>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
