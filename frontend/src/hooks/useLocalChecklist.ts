'use client';

import { useCallback, useEffect, useState } from 'react';

/**
 * Checklist state stored on this device, scoped per user.
 * Server-side persistence is a natural next step; the UI tells users it is device-local.
 */
export function useLocalChecklist(namespace: string, userId: string | undefined) {
  const key = `taxnavi.${namespace}.${userId ?? 'anon'}`;
  const [checked, setChecked] = useState<Record<string, boolean>>({});

  useEffect(() => {
    try {
      setChecked(JSON.parse(window.localStorage.getItem(key) || '{}'));
    } catch {
      setChecked({});
    }
  }, [key]);

  const toggle = useCallback(
    (id: string, value?: boolean) => {
      setChecked((current) => {
        const next = { ...current, [id]: value ?? !current[id] };
        window.localStorage.setItem(key, JSON.stringify(next));
        return next;
      });
    },
    [key],
  );

  return { checked, toggle };
}
