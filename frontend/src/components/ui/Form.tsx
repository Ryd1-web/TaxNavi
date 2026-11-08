'use client';

import { ReactNode, useEffect, useId, useState } from 'react';
import { formatMoneyInput, parseMoneyInput } from '@/lib/formatters';
import { Check } from './Icons';

interface FieldProps {
  label: string;
  hint?: ReactNode;
  error?: string;
  optional?: boolean;
  children: (ids: { id: string; describedBy?: string; invalid: boolean }) => ReactNode;
}

/** Label + control + hint/error, wired up with aria-describedby. */
export function Field({ label, hint, error, optional, children }: FieldProps) {
  const id = useId();
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [errorId, hintId].filter(Boolean).join(' ') || undefined;

  return (
    <div>
      <label htmlFor={id} className="label">
        {label}
        {optional && <span className="ml-1.5 font-normal text-ink-subtle">(optional)</span>}
      </label>
      {children({ id, describedBy, invalid: !!error })}
      {error ? (
        <p id={errorId} className="error-text" role="alert">
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="hint">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

interface MoneyInputProps {
  label: string;
  value: number;
  onChange: (value: number) => void;
  hint?: ReactNode;
  error?: string;
  optional?: boolean;
  placeholder?: string;
  suffix?: string;
  autoFocus?: boolean;
}

/**
 * Naira amount input. Accepts "1,500,000", "1.5m" or "850k", formats on blur,
 * and uses a numeric keypad on mobile.
 */
export function MoneyInput({ label, value, onChange, hint, error, optional, placeholder = '0', suffix, autoFocus }: MoneyInputProps) {
  const [text, setText] = useState(formatMoneyInput(value));
  const [focused, setFocused] = useState(false);
  const [localError, setLocalError] = useState<string>();

  useEffect(() => {
    if (!focused) setText(formatMoneyInput(value));
  }, [value, focused]);

  return (
    <Field label={label} hint={hint} error={error || localError} optional={optional}>
      {({ id, describedBy, invalid }) => (
        <div className="relative">
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[15px] text-ink-subtle">₦</span>
          <input
            id={id}
            type="text"
            inputMode="decimal"
            autoComplete="off"
            autoFocus={autoFocus}
            className={`input figure pl-8 ${suffix ? 'pr-20' : ''}`}
            placeholder={placeholder}
            value={text}
            aria-invalid={invalid || undefined}
            aria-describedby={describedBy}
            onFocus={() => setFocused(true)}
            onChange={(event) => {
              const raw = event.target.value;
              setText(raw);
              const parsed = parseMoneyInput(raw);
              if (Number.isNaN(parsed)) {
                setLocalError('Enter an amount in naira, e.g. 450,000 or 1.2m');
                return;
              }
              setLocalError(undefined);
              onChange(parsed);
            }}
            onBlur={() => {
              setFocused(false);
              if (!localError) setText(formatMoneyInput(value));
            }}
          />
          {suffix && (
            <span className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3.5 text-[13px] text-ink-subtle">{suffix}</span>
          )}
        </div>
      )}
    </Field>
  );
}

interface TextInputProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'id'> {
  label: string;
  hint?: ReactNode;
  error?: string;
  optional?: boolean;
}

export function TextInput({ label, hint, error, optional, className, ...props }: TextInputProps) {
  return (
    <Field label={label} hint={hint} error={error} optional={optional}>
      {({ id, describedBy, invalid }) => (
        <input id={id} className={`input ${className ?? ''}`} aria-invalid={invalid || undefined} aria-describedby={describedBy} {...props} />
      )}
    </Field>
  );
}

interface SelectProps extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, 'id'> {
  label: string;
  hint?: ReactNode;
  error?: string;
  options: { value: string | number; label: string }[];
}

export function Select({ label, hint, error, options, className, ...props }: SelectProps) {
  return (
    <Field label={label} hint={hint} error={error}>
      {({ id, describedBy, invalid }) => (
        <select id={id} className={`input appearance-none bg-[length:16px] bg-[right_0.875rem_center] bg-no-repeat pr-10 ${className ?? ''}`}
          style={{ backgroundImage: "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%2364748b' stroke-width='2'%3E%3Cpath d='M6 9l6 6 6-6'/%3E%3C/svg%3E\")" }}
          aria-invalid={invalid || undefined} aria-describedby={describedBy} {...props}>
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </Field>
  );
}

interface ChoiceCardProps {
  type?: 'radio' | 'checkbox';
  name: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  title: string;
  description?: string;
  icon?: ReactNode;
}

/** Large, touch-friendly radio/checkbox card. Uses a real input for keyboard and screen-reader support. */
export function ChoiceCard({ type = 'radio', name, checked, onChange, title, description, icon }: ChoiceCardProps) {
  return (
    <label
      className={`group relative flex cursor-pointer items-start gap-3.5 rounded-xl border bg-surface p-4 transition-colors has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-brand-600 has-[:focus-visible]:ring-offset-2 ${
        checked ? 'border-brand-600 bg-brand-50/40 shadow-[inset_0_0_0_1px_theme(colors.brand.600)]' : 'border-line-strong hover:border-ink-subtle'
      }`}
    >
      <input type={type} name={name} checked={checked} onChange={(event) => onChange(event.target.checked)} className="sr-only" />
      {icon && (
        <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${checked ? 'bg-brand-700 text-white' : 'bg-surface-muted text-ink-muted'}`}>
          {icon}
        </span>
      )}
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-semibold text-ink">{title}</span>
        {description && <span className="mt-0.5 block text-[13px] leading-5 text-ink-subtle">{description}</span>}
      </span>
      <span
        aria-hidden="true"
        className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center border transition-colors ${type === 'radio' ? 'rounded-full' : 'rounded-md'} ${
          checked ? 'border-brand-700 bg-brand-700 text-white' : 'border-line-strong bg-surface'
        }`}
      >
        {checked && <Check size={13} strokeWidth={3} />}
      </span>
    </label>
  );
}

interface ToggleProps {
  label: string;
  description?: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}

export function Toggle({ label, description, checked, onChange }: ToggleProps) {
  const id = useId();
  return (
    <div className="flex items-start justify-between gap-4">
      <div>
        <label htmlFor={id} className="text-sm font-medium text-ink">
          {label}
        </label>
        {description && <p className="mt-0.5 text-[13px] leading-5 text-ink-subtle">{description}</p>}
      </div>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative mt-0.5 inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors ${checked ? 'bg-brand-700' : 'bg-line-strong'}`}
      >
        <span className={`inline-block h-5 w-5 transform rounded-full bg-[#fff] shadow transition-transform ${checked ? 'translate-x-5' : 'translate-x-0.5'}`} />
      </button>
    </div>
  );
}
