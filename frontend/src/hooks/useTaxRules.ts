'use client';

import { useEffect, useState } from 'react';
import { apiService } from '@/services/apiService';
import { TaxRules } from '@/types';

const cache = new Map<string, Promise<TaxRules>>();

function fetchRules(year?: number) {
  const key = String(year ?? 'default');
  if (!cache.has(key)) {
    const request = apiService.rules(year);
    request.catch(() => cache.delete(key));
    cache.set(key, request);
  }
  return cache.get(key)!;
}

/** Loads the versioned tax rules (bands, thresholds, deadlines, pricing) from the API. */
export function useTaxRules(year?: number) {
  const [rules, setRules] = useState<TaxRules | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setError(null);
    fetchRules(year)
      .then((value) => active && setRules(value))
      .catch((err: Error) => active && setError(err.message));
    return () => {
      active = false;
    };
  }, [year]);

  return { rules, error, isLoading: !rules && !error };
}
