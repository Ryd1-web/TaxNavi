'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { User } from '@/types';

interface NavigationProps {
  user: User | null;
  onLogout: () => void;
}

export default function Navigation({ user, onLogout }: NavigationProps) {
  const router = useRouter();

  const handleLogout = () => {
    onLogout();
    router.push('/');
  };

  return (
    <nav className="bg-white shadow-md">
      <div className="container mx-auto px-4 py-4 flex justify-between items-center">
        <Link href="/" className="text-2xl font-bold text-blue-600">
          TaxNavi.ai
        </Link>

        <div className="flex items-center gap-6">
          <Link href="/dashboard" className="hover:text-blue-600">
            Dashboard
          </Link>
          <Link href="/tax/individual" className="hover:text-blue-600">
            Individual Tax
          </Link>
          <Link href="/tax/sme" className="hover:text-blue-600">
            SME Tax
          </Link>
          <Link href="/reports" className="hover:text-blue-600">
            Reports
          </Link>

          <div className="border-l pl-6 flex items-center gap-4">
            <span className="text-sm text-gray-600">{user?.firstName}</span>
            <button
              onClick={handleLogout}
              className="btn btn-secondary text-sm"
            >
              Logout
            </button>
          </div>
        </div>
      </div>
    </nav>
  );
}
