'use client';

import { usePathname, useSearchParams } from 'next/navigation';
import { useEffect, useRef, useState } from 'react';
import { progress } from '@/lib/progress';

// Data requests that finish faster than this never show the bar (avoids flicker).
const REQUEST_SHOW_DELAY_MS = 150;

/** Thin, faint top progress bar for route changes and data loading. */
export default function NavigationProgress() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [value, setValue] = useState(0);
  const [visible, setVisible] = useState(false);
  const trickle = useRef<ReturnType<typeof setInterval>>();
  const showDelay = useRef<ReturnType<typeof setTimeout>>();
  const hide = useRef<ReturnType<typeof setTimeout>>();
  const running = useRef(false);

  // Start navigation feedback the instant an internal link is clicked.
  useEffect(() => {
    const onClick = (event: MouseEvent) => {
      if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as Element | null)?.closest?.('a[href]') as HTMLAnchorElement | null;
      if (!anchor || anchor.hasAttribute('download') || (anchor.target && anchor.target !== '_self')) return;

      const url = new URL(anchor.href, window.location.href);
      if (url.origin !== window.location.origin) return;
      // Same page, or only the #hash changes: no route load happens.
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;

      progress.startNavigation();
    };
    document.addEventListener('click', onClick, true);
    return () => document.removeEventListener('click', onClick, true);
  }, []);

  // The route committed: navigation is done.
  useEffect(() => {
    progress.finishNavigation();
  }, [pathname, searchParams]);

  useEffect(() => {
    const start = () => {
      if (running.current) return;
      running.current = true;
      clearTimeout(hide.current);
      setVisible(true);
      setValue(0.12);
      clearInterval(trickle.current);
      // Ease towards 90% while waiting; never claims completion early.
      trickle.current = setInterval(() => setValue((v) => v + (0.9 - v) * 0.12), 250);
    };

    const finish = () => {
      clearTimeout(showDelay.current);
      if (!running.current) return;
      running.current = false;
      clearInterval(trickle.current);
      setValue(1);
      hide.current = setTimeout(() => {
        setVisible(false);
        hide.current = setTimeout(() => setValue(0), 250);
      }, 200);
    };

    const sync = () => {
      if (progress.navigating) {
        clearTimeout(showDelay.current);
        start();
      } else if (progress.loading) {
        if (!running.current) {
          clearTimeout(showDelay.current);
          showDelay.current = setTimeout(() => (progress.loading || progress.navigating) && start(), REQUEST_SHOW_DELAY_MS);
        }
      } else {
        finish();
      }
    };

    const unsubscribe = progress.subscribe(sync);
    return () => {
      unsubscribe();
      clearInterval(trickle.current);
      clearTimeout(showDelay.current);
      clearTimeout(hide.current);
    };
  }, []);

  return (
    <div className="nav-progress" aria-hidden="true">
      <div
        className="nav-progress__bar"
        // Appear instantly; only the fade-out is animated.
        style={{ transform: `scaleX(${value})`, opacity: visible ? 0.85 : 0, transitionDuration: visible ? '200ms, 0ms' : '200ms, 250ms' }}
      />
    </div>
  );
}
