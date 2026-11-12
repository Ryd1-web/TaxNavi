'use client';

import { FormEvent, useEffect, useRef, useState } from 'react';
import { apiService, ApiError } from '@/services/apiService';
import { AiChatResponse, AiCitation, AiGrounding, TaxCalculationRequest } from '@/types';
import { Button } from '@/components/ui/Button';
import { AlertTriangle, ArrowRight, MessageCircle, Plus } from '@/components/ui/Icons';
import { AnswerText, ConfidenceBadge, SourceList } from './AiSources';

const DEFAULT_SUGGESTIONS = [
  'What is the Nigeria Tax Act 2025?',
  'What deductions can I claim?',
  'How does rent relief work?',
  'Do I need to register for VAT?',
  'When do I file my annual return?',
];

/** Guest conversation continuity across pages (cleared when the browser session ends). */
const SESSION_KEY = 'taxnavi.ai.chat';

interface ThreadMessage {
  role: 'user' | 'assistant';
  content: string;
  citations: AiCitation[];
  grounding?: AiGrounding | null;
  confidence?: string;
  requiresProfessionalAdvice?: boolean;
}

interface Props {
  taxYear?: number;
  calculation?: TaxCalculationRequest;
  compact?: boolean;
}

/**
 * Ask TaxNavi — a conversational assistant grounded in Nigerian tax legislation (RAG).
 * Answers cite the Act and section they come from; when the AI service is offline the
 * server answers from its reviewed rule set instead, so the panel always works.
 */
export default function AssistantPanel({ taxYear, calculation, compact }: Props) {
  const [question, setQuestion] = useState('');
  const [messages, setMessages] = useState<ThreadMessage[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [guestToken, setGuestToken] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<string[]>(DEFAULT_SUGGESTIONS);
  const [disclaimer, setDisclaimer] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastFailed, setLastFailed] = useState<string | null>(null);
  const endRef = useRef<HTMLDivElement>(null);

  // Restore a session conversation so navigating around doesn't lose the thread.
  useEffect(() => {
    try {
      const stored = window.sessionStorage.getItem(SESSION_KEY);
      if (!stored) return;
      const { id, token } = JSON.parse(stored);
      if (!id) return;
      apiService
        .aiConversation(id, token ?? undefined)
        .then((conversation) => {
          setConversationId(id);
          setGuestToken(token ?? null);
          setMessages(
            conversation.messages.map((m) => ({
              role: m.role,
              content: m.content,
              citations: m.citations ?? [],
              grounding: m.grounding,
            })),
          );
        })
        .catch(() => window.sessionStorage.removeItem(SESSION_KEY));
    } catch {
      /* session storage unavailable */
    }
  }, []);

  useEffect(() => {
    if (messages.length > 0) endRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, [messages, loading]);

  const remember = (id: string, token: string | null) => {
    try {
      window.sessionStorage.setItem(SESSION_KEY, JSON.stringify({ id, token }));
    } catch {
      /* ignore */
    }
  };

  const reset = () => {
    setMessages([]);
    setConversationId(null);
    setGuestToken(null);
    setError(null);
    setLastFailed(null);
    try {
      window.sessionStorage.removeItem(SESSION_KEY);
    } catch {
      /* ignore */
    }
  };

  const ask = async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || loading) return;
    setLoading(true);
    setError(null);
    setLastFailed(null);
    setMessages((current) => [...current, { role: 'user', content: trimmed, citations: [] }]);
    setQuestion('');
    try {
      const response: AiChatResponse = await apiService.aiChat({
        question: trimmed,
        conversationId: conversationId ?? undefined,
        guestToken: guestToken ?? undefined,
        taxYear,
        calculation,
      });
      setConversationId(response.conversationId);
      const token = response.guestToken ?? guestToken;
      if (response.guestToken) setGuestToken(response.guestToken);
      remember(response.conversationId, token ?? null);
      if (response.suggestedQuestions?.length) setSuggestions(response.suggestedQuestions);
      setDisclaimer(response.disclaimer);
      setMessages((current) => [
        ...current,
        {
          role: 'assistant',
          content: response.answer,
          citations: response.citations ?? [],
          grounding: response.grounding,
          confidence: response.confidence,
          requiresProfessionalAdvice: response.requiresProfessionalAdvice,
        },
      ]);
    } catch (err) {
      const apiError = err as ApiError;
      setMessages((current) => current.slice(0, -1)); // the question wasn't answered; let the user retry it
      setError(apiError.message);
      setLastFailed(trimmed);
      if (apiError.status === 404) reset(); // stale stored conversation
    } finally {
      setLoading(false);
    }
  };

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    ask(question);
  };

  const visibleSuggestions = suggestions.filter((s) => calculation || s !== 'Explain my result').slice(0, 5);

  return (
    <section aria-labelledby="assistant-title" className={compact ? '' : 'card p-5 sm:p-6'}>
      <div className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand-700">
          <MessageCircle />
        </span>
        <div className="min-w-0 flex-1">
          <h2 id="assistant-title" className="text-base font-semibold">Ask TaxNavi</h2>
          <p className="text-sm text-ink-subtle">Answers grounded in Nigerian tax law, with the sections they come from.</p>
        </div>
        {messages.length > 0 && (
          <button
            type="button"
            onClick={reset}
            className="flex min-h-[36px] items-center gap-1 rounded-lg px-2 text-[13px] font-medium text-ink-subtle hover:text-ink"
            title="Start a new conversation"
          >
            <Plus size={15} /> New chat
          </button>
        )}
      </div>

      {messages.length > 0 && (
        <div className="mt-4 max-h-[26rem] space-y-3 overflow-y-auto pr-1" aria-live="polite">
          {messages.map((message, index) =>
            message.role === 'user' ? (
              <p key={index} className="ml-8 rounded-xl rounded-br-sm bg-brand-50 px-3.5 py-2 text-[14px] leading-6 text-ink">
                {message.content}
              </p>
            ) : (
              <div key={index} className="animate-fade-in rounded-xl rounded-bl-sm border border-line bg-surface-subtle p-3.5">
                <AnswerText text={message.content} citations={message.citations} />
                {message.requiresProfessionalAdvice && (
                  <p className="mt-2 flex gap-2 rounded-lg bg-amber-50 px-3 py-2 text-[13px] leading-5 text-amber-900">
                    <AlertTriangle size={16} className="mt-0.5 shrink-0" />
                    This needs advice specific to your situation. Speak to a chartered tax practitioner or your tax authority.
                  </p>
                )}
                <SourceList citations={message.citations} />
                {message.grounding && (
                  <div className="mt-2.5">
                    <ConfidenceBadge confidence={message.confidence ?? 'medium'} grounding={message.grounding} />
                  </div>
                )}
              </div>
            ),
          )}
          {loading && (
            <div className="flex items-center gap-2 pl-1 text-[13px] text-ink-subtle">
              <span className="flex gap-1">
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand-600 [animation-delay:0ms]" />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand-600 [animation-delay:150ms]" />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand-600 [animation-delay:300ms]" />
              </span>
              Checking the legislation…
            </div>
          )}
          <div ref={endRef} />
        </div>
      )}

      {error && (
        <div className="mt-3 flex items-start justify-between gap-2 rounded-lg bg-red-50 px-3 py-2 text-[13px] text-red-700 dark:bg-red-500/10 dark:text-red-300">
          <span>{error}</span>
          {lastFailed && (
            <button type="button" onClick={() => ask(lastFailed)} className="shrink-0 font-semibold hover:underline">
              Retry
            </button>
          )}
        </div>
      )}

      <div className="mt-4 flex flex-wrap gap-2">
        {visibleSuggestions.map((s) => (
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
          maxLength={1500}
          onChange={(event) => setQuestion(event.target.value)}
        />
        <Button type="submit" loading={loading} aria-label="Ask" className="shrink-0 px-3.5">
          {!loading && <ArrowRight />}
        </Button>
      </form>

      {disclaimer && <p className="mt-3 text-[11px] leading-4 text-ink-subtle">{disclaimer}</p>}
    </section>
  );
}
