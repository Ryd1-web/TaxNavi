import Link from 'next/link';
import { Logo } from './Logo';

const columns = [
  {
    title: 'Product',
    links: [
      { href: '/calculate', label: 'Tax calculator' },
      { href: '/calculate?type=sme', label: 'SME tax check' },
      { href: '/#pricing', label: 'Pricing' },
      { href: '/dashboard', label: 'Dashboard' },
    ],
  },
  {
    title: 'Learn',
    links: [
      { href: '/#how-it-works', label: 'How it works' },
      { href: '/#faq', label: 'FAQ' },
      { href: '/#trust', label: 'Security & accuracy' },
    ],
  },
];

export default function SiteFooter() {
  return (
    <footer className="border-t border-line bg-surface">
      <div className="container-page grid gap-10 py-12 md:grid-cols-[1.4fr_1fr_1fr]">
        <div className="max-w-sm">
          <Logo />
          <p className="mt-4 text-sm leading-6 text-ink-subtle">
            Your smarter way to understand, calculate and manage Nigerian taxes. Built for employees, freelancers and small businesses.
          </p>
        </div>
        {columns.map((column) => (
          <div key={column.title}>
            <h2 className="text-sm font-semibold text-ink">{column.title}</h2>
            <ul className="mt-3 space-y-2.5">
              {column.links.map((link) => (
                <li key={link.href}>
                  <Link href={link.href} className="text-sm text-ink-subtle hover:text-ink">
                    {link.label}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-line">
        <div className="container-page flex flex-col gap-2 py-6 text-xs leading-5 text-ink-subtle sm:flex-row sm:justify-between">
          <p>© {new Date().getFullYear()} TaxNavi.ai. Estimates only — not legal or tax advice.</p>
          <p>Confirm your filing position with the Nigeria Revenue Service, your State IRS or a qualified tax professional.</p>
        </div>
      </div>
    </footer>
  );
}
