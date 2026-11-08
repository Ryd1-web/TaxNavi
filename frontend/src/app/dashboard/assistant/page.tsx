'use client';

import AssistantPanel from '@/components/tax/AssistantPanel';
import { PageHeader } from '@/components/ui/Primitives';

export default function AssistantPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Ask TaxNavi"
        title="Tax questions, plain answers"
        description="Answers come only from TaxNavi's versioned tax rules. For penalties, disputes, foreign income or anything specific to your situation, you'll be pointed to a professional."
      />
      <div className="max-w-3xl">
        <AssistantPanel />
      </div>
    </div>
  );
}
