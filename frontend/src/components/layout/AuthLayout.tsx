import { ReactNode } from 'react';
import { Check } from '@/components/ui/Icons';
import { ThemeToggle } from '@/components/ui/ThemeToggle';
import { Logo } from './Logo';

/** Only allow same-site relative redirects (blocks "//evil.com" and absolute URLs). */
export function safeNext(value: string | null, fallback = '/dashboard') {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : fallback;
}

export default function AuthLayout({ title, subtitle, children }: { title: string; subtitle: ReactNode; children: ReactNode }) {
  return (
    <div className="grid min-h-screen lg:grid-cols-[1fr_minmax(0,520px)]">
      <div className="flex flex-col px-5 py-6 sm:px-10">
        <div className="flex items-center justify-between">
          <Logo />
          <ThemeToggle />
        </div>
        <main id="main" className="flex flex-1 items-center justify-center py-10">
          <div className="w-full max-w-[400px]">
            <h1 className="text-2xl font-semibold sm:text-[28px]">{title}</h1>
            <p className="mt-2 text-[15px] text-ink-muted">{subtitle}</p>
            <div className="mt-8">{children}</div>
          </div>
        </main>
      </div>
      <aside className="hidden flex-col justify-between bg-brand-900 p-12 text-white lg:flex">
        <p className="text-xs font-semibold uppercase tracking-[0.08em] text-gold-300">TaxNavi</p>
        <div>
          <p className="text-3xl font-semibold leading-tight text-white">Your smarter way to understand, calculate and manage Nigerian taxes.</p>
          <ul className="mt-8 space-y-3 text-brand-100">
            {['Saved reports and tax history', 'Deadlines tailored to you', 'Business compliance tools for SMEs'].map((item) => (
              <li key={item} className="flex items-center gap-2.5">
                <Check size={18} className="text-gold-300" />
                {item}
              </li>
            ))}
          </ul>
        </div>
        <p className="text-sm text-brand-200">Estimates only — not legal or tax advice.</p>
      </aside>
    </div>
  );
}
