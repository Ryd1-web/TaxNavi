'use client';

import { useEffect, useState } from 'react';
import { ThemePreference, useTheme } from '@/context/ThemeContext';
import { Monitor, Moon, Sun } from './Icons';

/** Header icon button: switches between light and dark. */
export function ThemeToggle({ className = '' }: { className?: string }) {
  const { resolved, toggle } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  const isDark = mounted && resolved === 'dark';
  const label = isDark ? 'Switch to light mode' : 'Switch to dark mode';

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={label}
      title={label}
      className={`flex h-10 w-10 items-center justify-center rounded-lg text-ink-muted transition-colors hover:bg-surface-muted hover:text-ink ${className}`}
    >
      {/* Render a stable icon until mounted to avoid a hydration mismatch. */}
      {isDark ? <Sun size={19} /> : <Moon size={19} />}
    </button>
  );
}

const options: { value: ThemePreference; label: string; icon: JSX.Element }[] = [
  { value: 'light', label: 'Light', icon: <Sun size={16} /> },
  { value: 'dark', label: 'Dark', icon: <Moon size={16} /> },
  { value: 'system', label: 'System', icon: <Monitor size={16} /> },
];

/** Settings control with an explicit "follow system" option. */
export function ThemeSelect() {
  const { preference, setPreference } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  return (
    <div role="radiogroup" aria-label="Theme" className="inline-flex flex-wrap gap-1 rounded-xl bg-surface-muted p-1">
      {options.map((option) => {
        const active = mounted && preference === option.value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => setPreference(option.value)}
            className={`flex min-h-[40px] items-center gap-2 rounded-lg px-3.5 text-sm font-medium transition-colors ${
              active ? 'bg-surface text-ink shadow-card' : 'text-ink-subtle hover:text-ink'
            }`}
          >
            {option.icon}
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
