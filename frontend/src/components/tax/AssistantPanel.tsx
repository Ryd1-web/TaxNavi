'use client';

import { FormEvent, useState } from 'react';
import { apiService, ApiError } from '@/services/apiService';
import { AssistantAnswer, TaxCalculationRequest } from '@/types';
import { Button } from '@/components/ui/Button';
import { AlertTriangle, ArrowRight, MessageCircle } from '@/components/ui/Icons';
import { VerificationBadge } from '@/components/ui/Primitives';

const DEFAULT_SUGGESTIONS = [
  'Explain my result',
  'What deductions can I claim?',
  'How does rent relief work?',
  'Do I need to register for VAT?',
  'When do I file my annual return?',
];

interface Props {
  taxYear?: number;
  calculation?: TaxCalculationRequest;
  compact?: boolean;
}

/**
 * Ask TaxNavi. Answers come from the server's grounded assistant, which only uses the
 * configured rule set and the deterministic engine — never invented rates or rules.
 */
export default function AssistantPanel({ taxYear, calculation, compact }: Props) {
  const [question, setQuestion] = useState('');
  const [asked, setAsked] = useState<string | null>(null);
  const [answer, setAnswer] = useState<AssistantAnswer | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const suggestions = (answer?.suggestedQuestions?.length ? answer.suggestedQuestions : DEFAULT_SUGGESTIONS).filter(
    (s) => calculation || s !== 'Explain my result',
  );

  const ask = async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed) return;
    setLoading(true);
    setError(null);
    setAsked(trimmed);
    try {
      setAnswer(await apiService.ask(trimmed, { taxYear, calculation }));
      setQuestion('');
    } catch (err) {
      setError((err as ApiError).message);
    } finally {
      setLoading(false);
    }
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    ask(question);
  };

  return (
    <section aria-labelledby="assistant-title" className={compact ? '' : 'card p-5 sm:p-6'}>
      <div className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
          <MessageCircle />
        </span>
        <div>
          <h2 id="assistant-title" className="text-base font-semibold">Ask TaxNavi</h2>
          <p className="text-sm text-ink-subtle">Plain-English answers grounded in the rules behind your calculation.</p>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {suggestions.slice(0, 5).map((s) => (
          <button
            key={s}
            type="button"
            onClick={() => ask(s)}
            disabled={loading}
            className="min-h-[36px] rounded-full border border-line-strong bg-surface px-3 text-[13px] font-medium text-ink-muted transition-colors hover:border-brand-600 hover:text-brand-800 disabled:opacity-60"
          >
            {s}
          </button>
        ))}
      </div>

      <form onSubmit={onSubmit} className="mt-3 flex gap-2">
        <label htmlFor="assistant-question" className="sr-only">Your tax question</label>
        <input
          id="assistant-question"
          className="input"
          placeholder="e.g. Is my pension deductible?"
          value={question}
          maxLength={500}
          onChange={(event) => setQuestion(event.target.value)}
        />
        <Button type="submit" loading={loading} aria-label="Ask" className="shrink-0 px-3.5">
          {!loading && <ArrowRight />}
        </Button>
      </form>

      <div aria-live="polite" className="mt-4 empty:hidden">
        {error && <p className="text-sm text-red-600">{error}</p>}
        {answer && !error && (
          <div className="animate-fade-in rounded-xl border border-line bg-surface-subtle p-4">
            {asked && <p className="text-[13px] font-medium text-ink-subtle">“{asked}”</p>}
            <p className="mt-2 whitespace-pre-line text-[15px] leading-7 text-ink">{answer.answer}</p>
            {answer.requiresProfessionalAdvice && (
              <p className="mt-3 flex gap-2 rounded-lg bg-amber-50 px-3 py-2 text-[13px] leading-5 text-amber-900">
                <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                This needs advice specific to your situation. Speak to a chartered tax practitioner or your tax authority.
              </p>
            )}
            <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-ink-subtle">
              <span>Based on: {answer.grounding}</span>
              {answer.topic !== 'Not covered' && <VerificationBadge status={answer.verificationStatus} />}
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
