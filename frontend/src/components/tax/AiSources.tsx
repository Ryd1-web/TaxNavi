'use client';

import { ReactNode, useState } from 'react';
import { AiCitation } from '@/types';
import { ChevronDown, ExternalLink } from '@/components/ui/Icons';

/** Renders answer text, turning [S1]-style markers into small source badges. */
export function AnswerText({ text, citations }: { text: string; citations: AiCitation[] }) {
  const known = new Set(citations.map((c) => c.reference));
  const parts: ReactNode[] = [];
  const pattern = /\[(S\d{1,2})\]/g;
  let last = 0;
  let match: RegExpExecArray | null;
  let key = 0;
  while ((match = pattern.exec(text)) !== null) {
    if (match.index > last) parts.push(text.slice(last, match.index));
    if (known.has(match[1])) {
      parts.push(
        <sup key={key++} className="mx-0.5 inline-flex items-center rounded bg-brand-50 px-1 text-[10px] font-semibold text-brand-700">
          {match[1]}
        </sup>,
      );
    }
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return <p className="whitespace-pre-line text-[15px] leading-7 text-ink">{parts}</p>;
}

function pages(citation: AiCitation) {
  if (!citation.pageFrom) return null;
  return citation.pageTo > citation.pageFrom ? `pp. ${citation.pageFrom}–${citation.pageTo}` : `p. ${citation.pageFrom}`;
}

/** Expandable list of the legislation/guidance passages behind an answer. */
export function SourceList({ citations }: { citations: AiCitation[] }) {
  const [open, setOpen] = useState<string | null>(null);
  if (citations.length === 0) return null;

  return (
    <div className="mt-3 space-y-1.5">
      <p className="text-xs font-semibold uppercase tracking-wide text-ink-subtle">Sources</p>
      {citations.map((citation) => {
        const expanded = open === citation.reference;
        const where = [
          citation.sectionNumber && `Section ${citation.sectionNumber}${citation.sectionTitle ? ` — ${citation.sectionTitle}` : ''}`,
          citation.part,
          pages(citation),
        ]
          .filter(Boolean)
          .join(' · ');
        return (
          <div key={citation.reference} className="rounded-lg border border-line bg-surface">
            <button
              type="button"
              onClick={() => setOpen(expanded ? null : citation.reference)}
              aria-expanded={expanded}
              className="flex w-full items-start gap-2 px-3 py-2 text-left"
            >
              <span className="mt-0.5 rounded bg-brand-50 px-1 text-[10px] font-semibold text-brand-700">{citation.reference}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13px] font-medium text-ink">{citation.documentTitle}</span>
                <span className="block truncate text-xs text-ink-subtle">
                  {where || citation.sourceName}
                  <span className={`ml-2 rounded px-1 text-[10px] font-medium ${citation.documentType === 'Legislation' ? 'bg-brand-50 text-brand-700' : 'bg-gold-50 text-gold-800'}`}>
                    {citation.documentType === 'Legislation' ? 'Binding law' : 'Official guidance'}
                  </span>
                </span>
              </span>
              <ChevronDown size={16} className={`mt-1 shrink-0 text-ink-subtle transition-transform ${expanded ? 'rotate-180' : ''}`} />
            </button>
            {expanded && (
              <div className="border-t border-line px-3 py-2">
                <p className="text-[13px] leading-6 text-ink-muted">“{citation.snippet}”</p>
                {citation.sourceUrl && (
                  <a
                    href={citation.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-brand-700 hover:underline"
                  >
                    {citation.sourceName || 'Official source'} <ExternalLink size={12} />
                  </a>
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

export function ConfidenceBadge({ confidence, grounding }: { confidence: string; grounding: string }) {
  const label =
    grounding === 'retrieval'
      ? { high: 'Strong sources', medium: 'Moderate sources', low: 'Weak sources' }[confidence] ?? 'Sources'
      : grounding === 'rules'
        ? 'TaxNavi reviewed rules'
        : grounding === 'escalation'
          ? 'Needs a professional'
          : 'Not enough evidence';
  const tone =
    grounding === 'retrieval' && confidence === 'high'
      ? 'bg-emerald-50 text-emerald-700'
      : grounding === 'escalation' || grounding === 'insufficient'
        ? 'bg-amber-50 text-amber-900'
        : 'bg-surface-muted text-ink-subtle';
  return <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>{label}</span>;
}
