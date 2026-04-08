'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { apiService } from '@/services/apiService';
import Navigation from '@/components/Navigation';
import { IndividualTaxData, TaxCalculationResult } from '@/types';

export default function IndividualTaxPage() {
  const router = useRouter();
  const { isAuthenticated, user, logout } = useAuth();
  const [formData, setFormData] = useState<IndividualTaxData>({
    userId: user?.id || '',
    monthlySalary: 0,
    annualBonus: 0,
    allowances: 0,
    businessIncome: 0,
    freelanceIncome: 0,
    investmentIncome: 0,
    rentPaid: 0,
    loanInterest: 0,
    otherExpenses: 0,
  });
  const [result, setResult] = useState<TaxCalculationResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isGeneratingReport, setIsGeneratingReport] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, router]);

  useEffect(() => {
    if (user?.id) {
      setFormData(prev => ({ ...prev, userId: user.id }));
    }
  }, [user?.id]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData({
      ...formData,
      [name]: parseFloat(value) || 0,
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const response = await apiService.assessIndividualTax(formData);
      setResult(response.data.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to calculate tax');
    } finally {
      setIsLoading(false);
    }
  };

  const handleGenerateReport = async () => {
    if (!result) return;
    
    setIsGeneratingReport(true);
    try {
      const response = await apiService.generatePdfReport({
        reportType: 'Individual',
        totalIncome: result.totalIncome,
        allowableDeductions: result.allowableDeductions,
        taxLiability: result.taxLiability,
        projectedSavings: result.projectedSavings,
      });
      
      if (response.data.data.pdfUrl) {
        // Construct the download URL using the API base URL (remove /api suffix)
        const apiBase = process.env.NEXT_PUBLIC_API_URL?.replace('/api', '') || 'http://localhost:5000';
        const downloadUrl = `${apiBase}${response.data.data.pdfUrl}`;
        
        // Fetch the PDF file directly from the backend
        const token = localStorage.getItem('jwtToken');
        const downloadResponse = await fetch(downloadUrl, {
          headers: {
            'Authorization': `Bearer ${token}`
          }
        });
        
        if (downloadResponse.ok) {
          const blob = await downloadResponse.blob();
          const blobUrl = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = blobUrl;
          link.download = response.data.data.fileName;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(blobUrl);
        } else {
          setError('Failed to download report');
        }
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to generate report');
    } finally {
      setIsGeneratingReport(false);
    }
  };

  if (!isAuthenticated) return null;

  return (
    <div>
      <Navigation user={user} onLogout={logout} />

      <div className="container py-8">
        <h1 className="text-4xl font-bold mb-8">Individual Tax Assessment</h1>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Form */}
          <div className="card">
            <h2 className="text-2xl font-bold mb-6">Income & Expenses</h2>
            
            {error && <div className="alert alert-error mb-4">{error}</div>}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="form-label">Monthly Salary (₦)</label>
                <input
                  type="number"
                  name="monthlySalary"
                  className="form-input"
                  value={formData.monthlySalary}
                  onChange={handleChange}
                  min="0"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Annual Bonus (₦)</label>
                  <input
                    type="number"
                    name="annualBonus"
                    className="form-input"
                    value={formData.annualBonus}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Allowances (₦)</label>
                  <input
                    type="number"
                    name="allowances"
                    className="form-input"
                    value={formData.allowances}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Business Income (₦)</label>
                  <input
                    type="number"
                    name="businessIncome"
                    className="form-input"
                    value={formData.businessIncome}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Freelance Income (₦)</label>
                  <input
                    type="number"
                    name="freelanceIncome"
                    className="form-input"
                    value={formData.freelanceIncome}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Investment Income (₦)</label>
                  <input
                    type="number"
                    name="investmentIncome"
                    className="form-input"
                    value={formData.investmentIncome}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Rent Paid (₦)</label>
                  <input
                    type="number"
                    name="rentPaid"
                    className="form-input"
                    value={formData.rentPaid}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Loan Interest (₦)</label>
                  <input
                    type="number"
                    name="loanInterest"
                    className="form-input"
                    value={formData.loanInterest}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Other Expenses (₦)</label>
                  <input
                    type="number"
                    name="otherExpenses"
                    className="form-input"
                    value={formData.otherExpenses}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <button
                type="submit"
                className="btn btn-primary w-full"
                disabled={isLoading}
              >
                {isLoading ? 'Calculating...' : 'Calculate Tax'}
              </button>
            </form>
          </div>

          {/* Results */}
          {result && (
            <div className="card bg-gradient-to-br from-blue-50 to-blue-100">
              <h2 className="text-2xl font-bold mb-6">Tax Calculation Results</h2>

              <div className="space-y-4 mb-6">
                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">Total Income</p>
                  <p className="text-2xl font-bold text-gray-800">₦{result.totalIncome.toLocaleString()}</p>
                </div>

                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">Allowable Deductions</p>
                  <p className="text-2xl font-bold text-gray-800">₦{result.allowableDeductions.toLocaleString()}</p>
                </div>

                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">Tax Liability</p>
                  <p className="text-3xl font-bold text-red-600">₦{result.taxLiability.toLocaleString()}</p>
                </div>

                <div className="bg-white rounded-lg p-4 border-2 border-green-400">
                  <p className="text-gray-600 text-sm">Projected Savings</p>
                  <p className="text-2xl font-bold text-green-600">₦{result.projectedSavings.toLocaleString()}</p>
                </div>
              </div>

              {result.aiSummary && (
                <div className="bg-white rounded-lg p-4 mb-4">
                  <h3 className="font-semibold mb-2">AI Summary</h3>
                  <p className="text-sm text-gray-700 whitespace-pre-wrap">{result.aiSummary}</p>
                </div>
              )}

              {result.savingsAdvice && (
                <div className="bg-white rounded-lg p-4 mb-4">
                  <h3 className="font-semibold mb-2">Optimization Tips</h3>
                  <p className="text-sm text-gray-700 whitespace-pre-wrap">{result.savingsAdvice}</p>
                </div>
              )}

              <button
                onClick={handleGenerateReport}
                disabled={isGeneratingReport}
                className="btn btn-success w-full"
              >
                {isGeneratingReport ? 'Generating Report...' : '📥 Generate & Download PDF Report'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
