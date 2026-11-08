'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { ReactNode, useEffect } from 'react';
import { useAuth } from '@/context/AuthContext';
import { ButtonLink } from '@/components/ui/Button';
import {
  Calendar,
  ClipboardCheck,
  CreditCard,
  FileText,
  Grid,
  LogOut,
  Menu,
  MessageCircle,
  Plus,
  Scale,
  Settings,
} from '@/components/ui/Icons';
import { Skeleton } from '@/components/ui/Primitives';
import { ThemeToggle } from '@/components/ui/ThemeToggle';
import { Logo } from './Logo';

interface NavItem {
  href: string;
  label: string;
  icon: ReactNode;
}

const navItems: NavItem[] = [
  { href: '/dashboard', label: 'Overview', icon: <Grid /> },
  { href: '/dashboard/reports', label: 'Reports', icon: <FileText /> },
  { href: '/dashboard/compliance', label: 'Compliance', icon: <Scale /> },
  { href: '/dashboard/calendar', label: 'Tax calendar', icon: <Calendar /> },
  { href: '/dashboard/documents', label: 'Documents', icon: <ClipboardCheck /> },
  { href: '/dashboard/assistant', label: 'Ask TaxNavi', icon: <MessageCircle /> },
  { href: '/dashboard/billing', label: 'Plan & billing', icon: <CreditCard /> },
  { href: '/dashboard/settings', label: 'Settings', icon: <Settings /> },
];

// Mobile tab bar: four key destinations plus "More" for everything else.
const mobileTabs: NavItem[] = [
  { href: '/dashboard', label: 'Overview', icon: <Grid /> },
  { href: '/dashboard/reports', label: 'Reports', icon: <FileText /> },
  { href: '/dashboard/calendar', label: 'Calendar', icon: <Calendar /> },
  { href: '/dashboard/assistant', label: 'Ask', icon: <MessageCircle /> },
  { href: '/dashboard/more', label: 'More', icon: <Menu /> },
];

function isActive(pathname: string, href: string) {
  return href === '/dashboard' ? pathname === href : pathname.startsWith(href);
}

/** Authenticated app layout: sidebar on desktop, top bar + bottom tabs on mobile. */
export default function AppShell({ children }: { children: ReactNode }) {
  const { user, isAuthenticated, isLoading, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace(`/auth/login?next=${encodeURIComponent(pathname)}`);
    }
  }, [isLoading, isAuthenticated, pathname, router]);

  if (isLoading || !isAuthenticated || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center" aria-busy="true" aria-live="polite">
        <div className="w-64 space-y-3">
          <Skeleton className="h-6 w-32" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
          <span className="sr-only">Loading your dashboard…</span>
        </div>
      </div>
    );
  }

  const signOut = () => {
    logout();
    router.push('/');
  };

  const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase() || user.email[0].toUpperCase();

  return (
    <div className="min-h-screen lg:grid lg:grid-cols-[248px_1fr]">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-3 focus:z-50 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2 focus:shadow-raised">
        Skip to content
      </a>

      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-screen flex-col border-r border-line bg-surface lg:flex">
        <div className="flex h-16 items-center justify-between pl-5 pr-3">
          <Logo href="/dashboard" />
          <ThemeToggle className="h-9 w-9" />
        </div>
        <div className="px-3 pb-3">
          <ButtonLink href="/calculate" block size="sm">
            <Plus size={16} /> New calculation
          </ButtonLink>
        </div>
        <nav aria-label="Dashboard" className="flex-1 space-y-0.5 overflow-y-auto px-3">
          {navItems.map((item) => {
            const active = isActive(pathname, item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? 'page' : undefined}
                className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                  active ? 'bg-surface-muted text-ink' : 'text-ink-muted hover:bg-surface-subtle hover:text-ink'
                }`}
              >
                <span className={active ? 'text-brand-700' : 'text-ink-subtle'}>{item.icon}</span>
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="border-t border-line p-3">
          <div className="flex items-center gap-3 rounded-lg px-2 py-2">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-800">{initials}</span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{`${user.firstName} ${user.lastName}`.trim() || user.email}</p>
              <p className="truncate text-xs text-ink-subtle">{user.userType === 'SME' ? 'Business account' : 'Personal account'}</p>
            </div>
            <button type="button" onClick={signOut} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-ink-subtle hover:bg-surface-muted hover:text-ink" aria-label="Sign out">
              <LogOut size={17} />
            </button>
          </div>
        </div>
      </aside>

      <div className="flex min-w-0 flex-col">
        {/* Mobile top bar */}
        <header className="sticky top-0 z-30 flex h-14 items-center justify-between border-b border-line bg-surface/90 px-4 backdrop-blur lg:hidden">
          <Logo href="/dashboard" />
          <div className="flex items-center gap-1">
            <ThemeToggle />
            <ButtonLink href="/calculate" size="sm">
              <Plus size={16} /> New
            </ButtonLink>
          </div>
        </header>

        <main id="main" className="flex-1 px-4 pb-28 pt-6 sm:px-6 lg:px-10 lg:pb-12 lg:pt-10">
          <div className="mx-auto w-full max-w-5xl">{children}</div>
        </main>

        {/* Mobile bottom tabs */}
        <nav aria-label="Dashboard" className="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden">
          <ul className="grid grid-cols-5">
            {mobileTabs.map((item) => {
              const href = item.href;
              const active = href === '/dashboard/more'
                ? ['/dashboard/compliance', '/dashboard/documents', '/dashboard/billing', '/dashboard/settings', '/dashboard/more'].some((p) => pathname.startsWith(p))
                : isActive(pathname, href);
              return (
                <li key={href}>
                  <Link
                    href={href}
                    aria-current={active ? 'page' : undefined}
                    className={`flex min-h-[56px] flex-col items-center justify-center gap-1 text-[11px] font-medium ${active ? 'text-brand-700' : 'text-ink-subtle'}`}
                  >
                    {item.icon}
                    {item.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      </div>
    </div>
  );
}

export { navItems };
