'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/context/AuthContext';
import { useEffect } from 'react';

export default function Home() {
  const router = useRouter();
  const { isAuthenticated } = useAuth();

  useEffect(() => {
    if (isAuthenticated) {
      router.push('/dashboard');
    }
  }, [isAuthenticated, router]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-600 to-blue-800 text-white">
      <div className="container mx-auto px-4 py-20">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-12 items-center">
          <div>
            <h1 className="text-5xl font-bold mb-6">TaxNavi.ai</h1>
            <p className="text-xl mb-4 text-blue-100">
              Advanced AI-powered tax advisory for individuals and SMEs in Nigeria
            </p>
            <p className="text-lg mb-8 text-blue-100">
              Get accurate tax calculations, intelligent recommendations, and comprehensive compliance guidance.
            </p>
            <div className="flex gap-4">
              <Link href="/auth/login" className="btn btn-primary">
                Login
              </Link>
              <Link href="/auth/register" className="btn bg-white text-blue-600 hover:bg-gray-100">
                Register
              </Link>
            </div>
          </div>
          <div className="bg-white bg-opacity-10 backdrop-blur-md rounded-lg p-8">
            <div className="space-y-6">
              <div className="flex items-start gap-4">
                <div className="text-3xl">📊</div>
                <div>
                  <h3 className="text-xl font-semibold mb-2">Accurate Tax Calculations</h3>
                  <p>Based on latest Nigerian tax regulations for PIT, VAT, and WHT</p>
                </div>
              </div>
              <div className="flex items-start gap-4">
                <div className="text-3xl">🤖</div>
                <div>
                  <h3 className="text-xl font-semibold mb-2">AI-Powered Insights</h3>
                  <p>Personalized tax optimization advice powered by Azure OpenAI</p>
                </div>
              </div>
              <div className="flex items-start gap-4">
                <div className="text-3xl">📄</div>
                <div>
                  <h3 className="text-xl font-semibold mb-2">Professional Reports</h3>
                  <p>Generate downloadable PDF reports with comprehensive summaries</p>
                </div>
              </div>
              <div className="flex items-start gap-4">
                <div className="text-3xl">🔒</div>
                <div>
                  <h3 className="text-xl font-semibold mb-2">Secure & Confidential</h3>
                  <p>Enterprise-grade security for your financial information</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
