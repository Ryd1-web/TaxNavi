'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { apiService } from '@/services/apiService';
import { TaxReport } from '@/types';
import Navigation from '@/components/Navigation';

export default function DashboardPage() {
  const router = useRouter();
  const { user, isAuthenticated, logout } = useAuth();
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
        <div className="mb-8">
          <h1 className="text-4xl font-bold mb-2">Welcome, {user?.firstName}!</h1>
          <p className="text-gray-600">Your tax dashboard</p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
          <Link href="/tax/individual" className="card hover:shadow-lg transition-shadow cursor-pointer">
            <div className="text-4xl mb-4">👤</div>
            <h3 className="text-xl font-semibold mb-2">Individual Tax Assessment</h3>
            <p className="text-gray-600">Calculate your personal tax liability</p>
          </Link>

          <Link href="/tax/sme" className="card hover:shadow-lg transition-shadow cursor-pointer">
            <div className="text-4xl mb-4">🏢</div>
            <h3 className="text-xl font-semibold mb-2">SME Tax Assessment</h3>
            <p className="text-gray-600">Get compliance roadmap for your business</p>
          </Link>

          <Link href="/reports" className="card hover:shadow-lg transition-shadow cursor-pointer">
            <div className="text-4xl mb-4">📋</div>
            <h3 className="text-xl font-semibold mb-2">View Reports</h3>
            <p className="text-gray-600">Access your tax assessment reports</p>
          </Link>
        </div>

        <div className="card">
          <h2 className="text-2xl font-bold mb-6">Recent Reports</h2>
          
          {isLoading ? (
            <p className="text-gray-600">Loading reports...</p>
          ) : reports.length === 0 ? (
            <p className="text-gray-600">No reports yet. Start a new tax assessment!</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead className="bg-gray-100">
                  <tr>
                    <th className="px-4 py-2 text-left">Type</th>
                    <th className="px-4 py-2 text-left">Tax Liability</th>
                    <th className="px-4 py-2 text-left">Total Income</th>
                    <th className="px-4 py-2 text-left">Date</th>
                    <th className="px-4 py-2 text-left">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {reports.map((report) => (
                    <tr key={report.id} className="border-b hover:bg-gray-50">
                      <td className="px-4 py-3">{report.reportType}</td>
                      <td className="px-4 py-3 font-semibold">₦{report.taxLiability.toLocaleString()}</td>
                      <td className="px-4 py-3">₦{report.totalIncome.toLocaleString()}</td>
                      <td className="px-4 py-3">{new Date(report.createdAt).toLocaleDateString()}</td>
                      <td className="px-4 py-3">
                        <Link href={`/reports/${report.id}`} className="text-blue-600 hover:underline">
                          View
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
