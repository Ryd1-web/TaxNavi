'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import { useAuth } from '@/context/AuthContext';
import { ButtonLink } from '@/components/ui/Button';
import { Menu, X } from '@/components/ui/Icons';
import { ThemeToggle } from '@/components/ui/ThemeToggle';
import { Logo } from './Logo';

const marketingLinks = [
  { href: '/#how-it-works', label: 'How it works' },
  { href: '/#solutions', label: 'Personal & SME' },
  { href: '/#pricing', label: 'Pricing' },
  { href: '/#faq', label: 'FAQ' },
];

export default function SiteHeader() {
  const { user, isAuthenticated, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => setOpen(false), [pathname]);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && setOpen(false);
    document.addEventListener('keydown', onKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', onKey);
      document.body.style.overflow = '';
    };
  }, [open]);

  const signOut = () => {
    logout();
    router.push('/');
  };

  return (
    <header className="sticky top-0 z-40 border-b border-line/80 bg-surface/85 backdrop-blur supports-[backdrop-filter]:bg-surface/70">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-3 focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2 focus:shadow-raised">
        Skip to content
      </a>
      <div className="container-page flex h-16 items-center justify-between gap-4">
        <Logo />

        <nav aria-label="Main" className="hidden items-center gap-1 md:flex">
          {marketingLinks.map((link) => (
            <Link key={link.href} href={link.href} className="rounded-lg px-3 py-2 text-sm font-medium text-ink-muted transition-colors hover:text-ink">
              {link.label}
            </Link>
          ))}
        </nav>

        <div className="hidden items-center gap-2 md:flex">
          <ThemeToggle />
          {isAuthenticated ? (
            <>
              <ButtonLink href="/dashboard" variant="ghost" size="sm">
                Dashboard
              </ButtonLink>
              <button type="button" onClick={signOut} className="btn btn-ghost btn-sm">
                Sign out
              </button>
            </>
          ) : (
            <ButtonLink href="/auth/login" variant="ghost" size="sm">
              Sign in
            </ButtonLink>
          )}
          <ButtonLink href="/calculate" size="sm">
            Calculate my tax
          </ButtonLink>
        </div>

        <div className="-mr-2 flex items-center gap-1 md:hidden">
          <ThemeToggle className="h-11 w-11" />
        <button
          type="button"
          className="flex h-11 w-11 items-center justify-center rounded-lg text-ink"
          aria-expanded={open}
          aria-controls="mobile-menu"
          aria-label={open ? 'Close menu' : 'Open menu'}
          onClick={() => setOpen((value) => !value)}
        >
          {open ? <X size={22} /> : <Menu size={22} />}
        </button>
        </div>
      </div>

      {open && (
        <div id="mobile-menu" className="fixed inset-x-0 bottom-0 top-16 z-40 overflow-y-auto border-t border-line bg-surface md:hidden">
          <nav aria-label="Mobile" className="container-page flex flex-col py-4">
            {marketingLinks.map((link) => (
              <Link key={link.href} href={link.href} className="rounded-lg px-2 py-3.5 text-base font-medium text-ink" onClick={() => setOpen(false)}>
                {link.label}
              </Link>
            ))}
            <div className="my-3 border-t border-line" />
            {isAuthenticated ? (
              <>
                <p className="px-2 pb-2 text-sm text-ink-subtle">Signed in as {user?.firstName || user?.email}</p>
                <Link href="/dashboard" className="rounded-lg px-2 py-3.5 text-base font-medium text-ink">
                  Dashboard
                </Link>
                <button type="button" onClick={signOut} className="rounded-lg px-2 py-3.5 text-left text-base font-medium text-ink">
                  Sign out
                </button>
              </>
            ) : (
              <Link href="/auth/login" className="rounded-lg px-2 py-3.5 text-base font-medium text-ink">
                Sign in
              </Link>
            )}
            <ButtonLink href="/calculate" size="lg" block className="mt-4">
              Calculate my tax
            </ButtonLink>
          </nav>
        </div>
      )}
    </header>
  );
}
