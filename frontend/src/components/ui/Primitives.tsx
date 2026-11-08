import { ReactNode } from 'react';
import { AlertTriangle, CheckCircle, Info, X } from './Icons';
import { VerificationStatus } from '@/types';

export function Card({ className = '', children, as: Tag = 'div' }: { className?: string; children: ReactNode; as?: 'div' | 'section' | 'article' }) {
  return <Tag className={`card ${className}`}>{children}</Tag>;
}

export function CardHeader({ title, description, action }: { title: ReactNode; description?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-3 border-b border-line px-5 py-4 sm:px-6">
      <div className="min-w-0">
        <h2 className="text-base font-semibold">{title}</h2>
        {description && <p className="mt-0.5 text-sm text-ink-subtle">{description}</p>}
      </div>
      {action}
    </div>
  );
}

type Tone = 'neutral' | 'brand' | 'gold' | 'success' | 'warning' | 'danger' | 'info';

const toneClasses: Record<Tone, string> = {
  neutral: 'bg-surface-muted text-ink-muted ring-line',
  brand: 'bg-brand-50 text-brand-800 ring-brand-200',
  gold: 'bg-gold-50 text-gold-800 ring-gold-200',
  success: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-800 ring-amber-200',
  danger: 'bg-red-50 text-red-700 ring-red-200',
  info: 'bg-sky-50 text-sky-800 ring-sky-200',
};

export function Badge({ tone = 'neutral', children, className = '' }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={`inline-flex items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${toneClasses[tone]} ${className}`}>
      {children}
    </span>
  );
}

const alertStyles = {
  info: { box: 'border-sky-200 bg-sky-50 text-sky-900', icon: <Info size={18} className="text-sky-700" /> },
  success: { box: 'border-emerald-200 bg-emerald-50 text-emerald-900', icon: <CheckCircle size={18} className="text-emerald-700" /> },
  warning: { box: 'border-amber-200 bg-amber-50 text-amber-900', icon: <AlertTriangle size={18} className="text-amber-700" /> },
  error: { box: 'border-red-200 bg-red-50 text-red-900', icon: <AlertTriangle size={18} className="text-red-700" /> },
};

export function Alert({
  tone = 'info',
  title,
  children,
  onDismiss,
  className = '',
}: {
  tone?: keyof typeof alertStyles;
  title?: ReactNode;
  children?: ReactNode;
  onDismiss?: () => void;
  className?: string;
}) {
  const style = alertStyles[tone];
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`flex gap-3 rounded-xl border px-4 py-3 text-sm ${style.box} ${className}`}>
      <span className="mt-0.5 shrink-0">{style.icon}</span>
      <div className="min-w-0 flex-1 leading-6">
        {title && <p className="font-semibold">{title}</p>}
        {children && <div className={title ? 'mt-0.5 opacity-90' : ''}>{children}</div>}
      </div>
      {onDismiss && (
        <button type="button" onClick={onDismiss} className="-m-1 h-8 w-8 shrink-0 rounded-lg p-1.5 opacity-70 hover:bg-ink/5 hover:opacity-100" aria-label="Dismiss">
          <X size={16} />
        </button>
      )}
    </div>
  );
}

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded-lg bg-line ${className}`} aria-hidden="true" />;
}

export function EmptyState({ icon, title, description, action }: { icon: ReactNode; title: string; description: string; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-6 py-14 text-center">
      <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-surface-muted text-ink-muted">{icon}</span>
      <h3 className="mt-4 text-base font-semibold">{title}</h3>
      <p className="mt-1 max-w-sm text-sm leading-6 text-ink-subtle">{description}</p>
      {action && <div className="mt-5">{action}</div>}
    </div>
  );
}

export function Stat({ label, value, sub, emphasis }: { label: string; value: ReactNode; sub?: ReactNode; emphasis?: boolean }) {
  return (
    <div className={`rounded-xl border p-4 ${emphasis ? 'border-brand-200 bg-brand-50/50' : 'border-line bg-surface'}`}>
      <p className="text-[13px] font-medium text-ink-subtle">{label}</p>
      <p className="figure mt-1 text-xl font-semibold text-ink sm:text-[22px]">{value}</p>
      {sub && <p className="mt-0.5 text-xs text-ink-subtle">{sub}</p>}
    </div>
  );
}

export function PageHeader({ eyebrow, title, description, actions }: { eyebrow?: string; title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1 className="mt-1 text-2xl font-semibold sm:text-[28px]">{title}</h1>
        {description && <p className="mt-1.5 max-w-2xl text-[15px] leading-6 text-ink-muted">{description}</p>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

const verificationCopy: Record<VerificationStatus, { tone: Tone; label: string; title: string }> = {
  Verified: { tone: 'success', label: 'Verified', title: 'Checked against official legislation or NRS guidance.' },
  SecondarySourcesVerified: { tone: 'brand', label: 'Source-checked', title: 'Consistent across professional adviser summaries; official text confirmation pending.' },
  PendingValidation: { tone: 'warning', label: 'Pending validation', title: 'Published guidance is inconsistent. Confirm with the NRS or a tax adviser.' },
};

export function VerificationBadge({ status }: { status: VerificationStatus }) {
  const copy = verificationCopy[status] ?? verificationCopy.PendingValidation;
  return (
    <Badge tone={copy.tone}>
      <span title={copy.title}>{copy.label}</span>
      <span className="sr-only">: {copy.title}</span>
    </Badge>
  );
}
