import Link from 'next/link';

export function LogoMark({ size = 28 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
      <rect width="32" height="32" rx="8" fill="#0b6b4f" />
      {/* Compass needle: navigation + direction */}
      <path d="M16 6.5l4.2 9.5L16 25.5 11.8 16 16 6.5z" fill="#fff" fillOpacity="0.95" />
      <path d="M16 6.5l4.2 9.5H11.8L16 6.5z" fill="#d6a446" />
      <circle cx="16" cy="16" r="1.6" fill="#0b6b4f" />
    </svg>
  );
}

export function Logo({ href = '/' }: { href?: string }) {
  return (
    <Link href={href} className="flex items-center gap-2.5 rounded-lg" aria-label="TaxNavi home">
      <LogoMark />
      <span className="text-[17px] font-semibold tracking-tight text-ink">
        TaxNavi<span className="text-brand-700">.ai</span>
      </span>
    </Link>
  );
}
