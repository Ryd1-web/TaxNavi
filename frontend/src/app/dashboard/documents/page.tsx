'use client';

import { Card, PageHeader } from '@/components/ui/Primitives';
import { useAuth } from '@/context/AuthContext';
import { useLocalChecklist } from '@/hooks/useLocalChecklist';
import { individualDocuments, smeDocuments } from '@/lib/checklists';

export default function DocumentsPage() {
  const { user } = useAuth();
  const isSme = user?.userType === 'SME';
  const items = isSme ? smeDocuments : individualDocuments;
  const { checked, toggle } = useLocalChecklist('documents', user?.id);
  const done = items.filter((item) => checked[item.id]).length;
  const groups = Array.from(new Set(items.map((item) => item.group)));

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Be ready to file"
        title="Document checklist"
        description={isSme ? 'Records your business should keep to file accurately and support every claim.' : 'What to gather before you file your annual return.'}
      />

      <Card className="p-5 sm:p-6">
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium">
            {done} of {items.length} ready
          </span>
          <span className="text-ink-subtle">Saved on this device</span>
        </div>
        <div className="mt-2 h-2 rounded-full bg-surface-muted" role="progressbar" aria-valuemin={0} aria-valuemax={items.length} aria-valuenow={done} aria-label="Documents ready">
          <div className="h-2 rounded-full bg-brand-600 transition-all" style={{ width: `${(done / items.length) * 100}%` }} />
        </div>
      </Card>

      {groups.map((group) => (
        <section key={group} aria-labelledby={`g-${group}`}>
          <h2 id={`g-${group}`} className="mb-2 text-sm font-semibold text-ink-subtle">
            {group}
          </h2>
          <Card>
            <ul className="divide-y divide-line">
              {items
                .filter((item) => item.group === group)
                .map((item) => (
                  <li key={item.id}>
                    <label className="flex cursor-pointer items-start gap-3 px-5 py-4 hover:bg-surface-subtle">
                      <input type="checkbox" checked={!!checked[item.id]} onChange={() => toggle(item.id)} className="mt-0.5 h-5 w-5 shrink-0 accent-brand-700" />
                      <span>
                        <span className={`block text-sm font-medium ${checked[item.id] ? 'text-ink-subtle line-through' : ''}`}>{item.title}</span>
                        <span className="mt-0.5 block text-[13px] leading-5 text-ink-subtle">{item.description}</span>
                      </span>
                    </label>
                  </li>
                ))}
            </ul>
          </Card>
        </section>
      ))}
    </div>
  );
}
