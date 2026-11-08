const nairaFormatter = new Intl.NumberFormat('en-NG', {
  style: 'currency',
  currency: 'NGN',
  currencyDisplay: 'narrowSymbol',
  maximumFractionDigits: 0,
});

const nairaPreciseFormatter = new Intl.NumberFormat('en-NG', {
  style: 'currency',
  currency: 'NGN',
  currencyDisplay: 'narrowSymbol',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const groupFormatter = new Intl.NumberFormat('en-NG', { maximumFractionDigits: 0 });

/** ₦1,234,567 — whole naira for display. */
export function formatCurrency(amount: number | null | undefined) {
  return nairaFormatter.format(amount || 0);
}

/** ₦1,234,567.89 — for line items where kobo matter. */
export function formatCurrencyPrecise(amount: number | null | undefined) {
  return nairaPreciseFormatter.format(amount || 0);
}

/** ₦1.2m / ₦850k — for tight spaces like chart labels. */
export function formatCurrencyCompact(amount: number) {
  const abs = Math.abs(amount);
  if (abs >= 1_000_000_000) return `₦${trim(amount / 1_000_000_000)}bn`;
  if (abs >= 1_000_000) return `₦${trim(amount / 1_000_000)}m`;
  if (abs >= 1_000) return `₦${trim(amount / 1_000)}k`;
  return formatCurrency(amount);
}

function trim(value: number) {
  return value.toFixed(value >= 100 ? 0 : 1).replace(/\.0$/, '');
}

/** 0.1234 → "12.34%"; whole rates render without decimals ("15%"). */
export function formatPercent(value: number, digits?: number) {
  const pct = (value || 0) * 100;
  const d = digits ?? (Number.isInteger(Math.round(pct * 100) / 100) ? 0 : 2);
  return `${pct.toFixed(d)}%`;
}

/** Digits-only grouping for money inputs: "1500000" → "1,500,000". */
export function formatMoneyInput(value: number) {
  return value ? groupFormatter.format(value) : '';
}

/** Parses user input like "₦1,500,000.50" or "1.5m" into a number. */
export function parseMoneyInput(raw: string): number {
  const cleaned = raw.toLowerCase().replace(/[₦,\s]/g, '').replace(/ngn/g, '');
  if (!cleaned) return 0;
  const match = cleaned.match(/^(\d*\.?\d*)([km]|bn)?$/);
  if (!match || match[1] === '' || match[1] === '.') return NaN;
  const base = parseFloat(match[1]);
  const multiplier = match[2] === 'k' ? 1_000 : match[2] === 'm' ? 1_000_000 : match[2] === 'bn' ? 1_000_000_000 : 1;
  return Math.round(base * multiplier * 100) / 100;
}

export function formatDate(value: string | Date, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }) {
  return new Intl.DateTimeFormat('en-NG', options).format(typeof value === 'string' ? new Date(value) : value);
}

export function taxpayerLabel(type: string) {
  switch (type) {
    case 'SelfEmployed':
      return 'Self-employed';
    case 'SME':
      return 'Business';
    default:
      return 'Employee';
  }
}
