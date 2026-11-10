'use client';

import { createContext, ReactNode, useCallback, useContext, useEffect, useLayoutEffect, useMemo, useState } from 'react';

export type ThemePreference = 'light' | 'dark' | 'system';
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'taxnavi.theme';
const MEDIA = '(prefers-color-scheme: dark)';

/**
 * Runs in <head> before first paint so the page never flashes the wrong theme.
 * Keep the logic in sync with `applyTheme` below.
 */
export const themeInitScript = `(function(){try{var p=localStorage.getItem('${STORAGE_KEY}');var d=p==='dark'||(p!=='light'&&window.matchMedia('${MEDIA}').matches);var r=document.documentElement;r.classList.toggle('dark',d);r.style.colorScheme=d?'dark':'light';}catch(e){}})();`;

interface ThemeContextValue {
  preference: ThemePreference;
  resolved: ResolvedTheme;
  setPreference: (preference: ThemePreference) => void;
  toggle: () => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

function readPreference(): ThemePreference {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : 'system';
  } catch {
    return 'system';
  }
}

function resolve(preference: ThemePreference): ResolvedTheme {
  if (preference !== 'system') return preference;
  return window.matchMedia(MEDIA).matches ? 'dark' : 'light';
}

function applyTheme(theme: ResolvedTheme) {
  const root = document.documentElement;
  // Suppress colour transitions for one frame so the whole UI switches at once.
  const style = document.createElement('style');
  style.textContent = '*,*::before,*::after{transition:none!important}';
  document.head.appendChild(style);
  root.classList.toggle('dark', theme === 'dark');
  root.style.colorScheme = theme;
  window.getComputedStyle(root).opacity; // force reflow before re-enabling transitions
  window.requestAnimationFrame(() => style.remove());
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>('system');
  const [resolved, setResolved] = useState<ResolvedTheme>('light');

  // Hydrate from storage and re-apply it: the init script paints the right theme first, but React's
  // hydration of <html> can drop the class it added, so the saved choice must be enforced here too.
  useLayoutEffect(() => {
    const stored = readPreference();
    const theme = resolve(stored);
    setPreferenceState(stored);
    setResolved(theme);
    const root = document.documentElement;
    root.classList.toggle('dark', theme === 'dark');
    root.style.colorScheme = theme;
  }, []);

  // Follow the operating system while the preference is "system".
  useEffect(() => {
    if (preference !== 'system') return;
    const media = window.matchMedia(MEDIA);
    const onChange = () => {
      const next = media.matches ? 'dark' : 'light';
      setResolved(next);
      applyTheme(next);
    };
    media.addEventListener('change', onChange);
    return () => media.removeEventListener('change', onChange);
  }, [preference]);

  // Keep other open tabs in sync.
  useEffect(() => {
    const onStorage = (event: StorageEvent) => {
      if (event.key !== STORAGE_KEY) return;
      const next = readPreference();
      setPreferenceState(next);
      const theme = resolve(next);
      setResolved(theme);
      applyTheme(theme);
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const setPreference = useCallback((next: ThemePreference) => {
    try {
      if (next === 'system') window.localStorage.removeItem(STORAGE_KEY);
      else window.localStorage.setItem(STORAGE_KEY, next);
    } catch {
      /* storage unavailable: theme still applies for this session */
    }
    const theme = resolve(next);
    setPreferenceState(next);
    setResolved(theme);
    applyTheme(theme);
  }, []);

  // Flip whatever is currently on screen and remember it as an explicit choice.
  const toggle = useCallback(
    () => setPreference(document.documentElement.classList.contains('dark') ? 'light' : 'dark'),
    [setPreference],
  );

  const value = useMemo(() => ({ preference, resolved, setPreference, toggle }), [preference, resolved, setPreference, toggle]);
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) throw new Error('useTheme must be used within a ThemeProvider');
  return context;
}
