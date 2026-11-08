'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { navItems } from '@/components/layout/AppShell';
import { ChevronRight, LogOut } from '@/components/ui/Icons';
import { Card, PageHeader } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';

export default function MorePage() {
  const { logout } = useAuth();
  const router = useRouter();

  return (
    <div className="space-y-6">
      <PageHeader title="Menu" />
      <Card>
        <ul className="divide-y divide-line">
          {navItems.map((item) => (
            <li key={item.href}>
              <Link href={item.href} className="flex min-h-[56px] items-center gap-3 px-5 text-[15px] font-medium hover:bg-surface-subtle">
                <span className="text-ink-subtle">{item.icon}</span>
                <span className="flex-1">{item.label}</span>
                <ChevronRight size={18} className="text-ink-subtle" />
              </Link>
            </li>
          ))}
          <li>
            <button
              type="button"
              onClick={() => {
                logout();
                router.push('/');
              }}
              className="flex min-h-[56px] w-full items-center gap-3 px-5 text-left text-[15px] font-medium text-red-700 hover:bg-red-50"
            >
              <LogOut />
              Sign out
            </button>
          </li>
        </ul>
      </Card>
    </div>
  );
}
