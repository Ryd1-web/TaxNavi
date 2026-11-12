'use client';

import { useState } from 'react';
import { apiService, ApiError } from '@/services/apiService';
import { ExplainReportResponse, TaxCalculationRequest } from '@/types';
import { Button } from '@/components/ui/Button';
import { Card } from '@/components/ui/Primitives';
import { MessageCircle } from '@/components/ui/Icons';
import { AnswerText, SourceList } from './AiSources';

interface Props {
  calculation: TaxCalculationRequest;
}

/**
 * "Explain my tax results": a plain-English walk-through of the figures on this page.
 * The numbers are recomputed by TaxNavi's engine on the server; the AI only explains them.
 */
export default function ExplainResults({ calculation }: Props) {
  const [result, setResult] = useState<ExplainReportResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const explain = async () => {
    setLoading(true);
    setError(null);
    try {
      setResult(await apiService.explainReport(calculation));
    } catch (err) {
      setError((err as ApiError).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card className="p-5 sm:p-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
            <MessageCircle />
          </span>
          <div>
            <h2 className="text-base font-semibold">Explain my results</h2>
            <p className="text-sm text-ink-subtle">
              A plain-English walk-through of how these figures were reached, with the law behind each step.
            </p>
          </div>
        </div>
        {!result && (
          <Button onClick={explain} loading={loading} loadingText="Reading the legislation…" variant="secondary">
            Explain with AI
          </Button>
        )}
      </div>

      {error && (
        <div className="mt-4 flex items-center justify-between gap-2 rounded-lg bg-red-50 px-3 py-2 text-[13px] text-red-700 dark:bg-red-500/10 dark:text-red-300">
          <span>{error}</span>
          <button type="button" onClick={explain} className="font-semibold hover:underline">Retry</button>
        </div>
      )}

      {result && (
        <div className="mt-4 animate-fade-in">
          <AnswerText text={result.explanation} citations={result.citations} />
          <SourceList citations={result.citations} />
          <p className="mt-3 text-[11px] leading-4 text-ink-subtle">{result.disclaimer}</p>
        </div>
      )}
    </Card>
  );
}
