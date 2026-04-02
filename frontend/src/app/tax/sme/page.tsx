'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { apiService } from '@/services/apiService';
import Navigation from '@/components/Navigation';
import { SMETaxData, SMETaxCalculationResult } from '@/types';

export default function SMETaxPage() {
  const router = useRouter();
  const { isAuthenticated, user, logout } = useAuth();
  const [formData, setFormData] = useState<SMETaxData>({
    userId: user?.id || '',
    businessName: '',
    businessType: '',
    annualTurnover: 0,
    operatingExpenses: 0,
    payrollCost: 0,
    rentAndUtilities: 0,
    assets: 0,
    vatableIncome: 0,
  });
  const [result, setResult] = useState<SMETaxCalculationResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
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

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFormData({
      ...formData,
      [name]: isNaN(parseFloat(value)) ? value : parseFloat(value),
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const response = await apiService.assessSMETax(formData);
      setResult(response.data.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to calculate tax');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isAuthenticated) return null;

  return (
    <div>
      <Navigation user={user} onLogout={logout} />

      <div className="container py-8">
        <h1 className="text-4xl font-bold mb-8">SME Tax Assessment</h1>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Form */}
          <div className="card">
            <h2 className="text-2xl font-bold mb-6">Business Information</h2>
            
            {error && <div className="alert alert-error mb-4">{error}</div>}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="form-label">Business Name</label>
                <input
                  type="text"
                  name="businessName"
                  className="form-input"
                  value={formData.businessName}
                  onChange={handleChange}
                  required
                />
              </div>

              <div>
                <label className="form-label">Business Type</label>
                <select
                  name="businessType"
                  className="form-input"
                  value={formData.businessType}
                  onChange={handleChange}
                  required
                >
                  <option value="">Select type</option>
                  <option value="Retail">Retail</option>
                  <option value="Service">Service</option>
                  <option value="Manufacturing">Manufacturing</option>
                  <option value="Technology">Technology</option>
                  <option value="Other">Other</option>
                </select>
              </div>

              <div>
                <label className="form-label">Annual Turnover (₦)</label>
                <input
                  type="number"
                  name="annualTurnover"
                  className="form-input"
                  value={formData.annualTurnover}
                  onChange={handleChange}
                  min="0"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Operating Expenses (₦)</label>
                  <input
                    type="number"
                    name="operatingExpenses"
                    className="form-input"
                    value={formData.operatingExpenses}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Payroll Cost (₦)</label>
                  <input
                    type="number"
                    name="payrollCost"
                    className="form-input"
                    value={formData.payrollCost}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="form-label">Rent & Utilities (₦)</label>
                  <input
                    type="number"
                    name="rentAndUtilities"
                    className="form-input"
                    value={formData.rentAndUtilities}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
                <div>
                  <label className="form-label">Assets Value (₦)</label>
                  <input
                    type="number"
                    name="assets"
                    className="form-input"
                    value={formData.assets}
                    onChange={handleChange}
                    min="0"
                  />
                </div>
              </div>

              <div>
                <label className="form-label">Vatable Income (₦)</label>
                <input
                  type="number"
                  name="vatableIncome"
                  className="form-input"
                  value={formData.vatableIncome}
                  onChange={handleChange}
                  min="0"
                />
              </div>

              <button
                type="submit"
                className="btn btn-primary w-full"
                disabled={isLoading}
              >
                {isLoading ? 'Calculating...' : 'Calculate Tax Obligations'}
              </button>
            </form>
          </div>

          {/* Results */}
          {result && (
            <div className="card bg-gradient-to-br from-green-50 to-green-100">
              <h2 className="text-2xl font-bold mb-6">Tax Obligations</h2>

              <div className="space-y-4 mb-6">
                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">VAT Status</p>
                  <p className="text-xl font-bold">
                    {result.vatEligible ? '✓ Registered' : '✗ Not Registered'}
                  </p>
                  {result.vatEligible && (
                    <p className="text-sm text-gray-600 mt-1">Turnover exceeds ₦25 million</p>
                  )}
                </div>

                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">VAT Liability</p>
                  <p className="text-2xl font-bold text-gray-800">₦{result.vatLiability.toLocaleString()}</p>
                </div>

                <div className="bg-white rounded-lg p-4">
                  <p className="text-gray-600 text-sm">WHT Liability</p>
                  <p className="text-2xl font-bold text-gray-800">₦{result.whtLiability.toLocaleString()}</p>
                </div>

                <div className="bg-white rounded-lg p-4 border-2 border-red-400">
                  <p className="text-gray-600 text-sm">Total Tax Obligation</p>
                  <p className="text-3xl font-bold text-red-600">₦{result.totalTaxObligation.toLocaleString()}</p>
                </div>
              </div>

              {result.complianceRoadmap && (
                <div className="bg-white rounded-lg p-4">
                  <h3 className="font-semibold mb-3">Compliance Roadmap</h3>
                  <p className="text-sm text-gray-700 whitespace-pre-wrap">{result.complianceRoadmap}</p>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
