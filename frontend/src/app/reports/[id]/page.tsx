'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter, useParams } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { apiService } from '@/services/apiService';
import Navigation from '@/components/Navigation';
import { TaxReport } from '@/types';

export default function ReportDetailPage() {
  const router = useRouter();
  const params = useParams();
  const reportId = params.id as string;
  const { isAuthenticated, user, logout } = useAuth();
  const [report, setReport] = useState<TaxReport | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/auth/login');
      return;
    }

    const fetchReport = async () => {
      try {
        const response = await apiService.getReport(reportId);
        setReport(response.data.data);
      } catch (error) {
        console.error('Failed to fetch report:', error);
      } finally {
        setIsLoading(false);
      }
    };

    if (reportId) {
      fetchReport();
    }
  }, [isAuthenticated, router, reportId]);

  const handleDownloadPDF = async () => {
    if (!report) return;

    try {
      const downloadUrl = report.pdfUrl || `/api/reports/download/${report.id}`;
      const apiBase = process.env.NEXT_PUBLIC_API_URL?.replace('/api', '') || 'http://localhost:5000';
      const fullUrl = downloadUrl.startsWith('http') ? downloadUrl : `${apiBase}${downloadUrl}`;

      // Fetch the PDF with Authorization header
      const token = localStorage.getItem('jwtToken');
      const downloadResponse = await fetch(fullUrl, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (downloadResponse.ok) {
        const blob = await downloadResponse.blob();
        const blobUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = blobUrl;
        link.download = `tax-report-${report.id}.pdf`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(blobUrl);
      } else {
        console.error('Download failed:', downloadResponse.status);
      }
    } catch (error) {
      console.error('Error downloading PDF:', error);
    }
  };

  if (!isAuthenticated) return null;

  if (isLoading) {
    return (
      <div>
        <Navigation user={user} onLogout={logout} />
        <div className="container py-8">
          <p className="text-center text-gray-600">Loading report...</p>
        </div>
      </div>
    );
  }

  if (!report) {
    return (
      <div>
        <Navigation user={user} onLogout={logout} />
        <div className="container py-8">
          <div className="card text-center py-12">
            <p className="text-gray-600 mb-4">Report not found</p>
            <Link href="/reports" className="btn btn-primary">
              Back to Reports
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div>
      <Navigation user={user} onLogout={logout} />

      <div className="container py-8">
        <div className="flex justify-between items-center mb-8">
          <h1 className="text-4xl font-bold">Tax Report</h1>
          <div className="flex gap-4">
            <button
              onClick={handleDownloadPDF}
              className="btn btn-primary"
              title="Download PDF Report"
            >
              📥 Download PDF
            </button>
            <Link href="/reports" className="btn btn-secondary">
              Back to Reports
            </Link>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          <div className="space-y-6">
            <div className="card">
              <h2 className="text-2xl font-bold mb-6">Assessment Details</h2>

              <div className="space-y-4">
                <div className="flex justify-between py-3 border-b">
                  <span className="text-gray-600">Report Type</span>
                  <span className="font-semibold">{report.reportType}</span>
                </div>

                <div className="flex justify-between py-3 border-b">
                  <span className="text-gray-600">Total Income</span>
                  <span className="font-semibold">₦{report.totalIncome.toLocaleString()}</span>
                </div>

                <div className="flex justify-between py-3 border-b">
                  <span className="text-gray-600">Allowable Deductions</span>
                  <span className="font-semibold">₦{report.allowableDeductions.toLocaleString()}</span>
                </div>

                <div className="flex justify-between py-3 border-b">
                  <span className="text-gray-600">Tax Liability</span>
                  <span className="font-semibold text-red-600">₦{report.taxLiability.toLocaleString()}</span>
                </div>

                <div className="flex justify-between py-3 bg-green-50 px-3 rounded">
                  <span className="text-gray-600">Projected Savings</span>
                  <span className="font-semibold text-green-600">₦{report.projectedSavings.toLocaleString()}</span>
                </div>
              </div>
            </div>
          </div>

          <div className="card">
            <h2 className="text-2xl font-bold mb-6">Report Summary</h2>
            <p className="text-gray-700 whitespace-pre-wrap text-sm leading-relaxed">
              {report.summary || 'No summary available'}
            </p>

            <hr className="my-6" />

            <p className="text-xs text-gray-500">
              Generated: {new Date(report.createdAt).toLocaleString()}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
