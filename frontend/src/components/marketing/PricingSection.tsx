'use client';

import { useTaxRules } from '@/hooks/useTaxRules';
import { formatCurrency } from '@/lib/formatters';
import { ButtonLink } from '@/components/ui/Button';
import { Check } from '@/components/ui/Icons';
import { Badge, Skeleton } from '@/components/ui/Primitives';

/** Prices are read from the API so the page always matches what checkout will charge. */
export default function PricingSection() {
  const { rules } = useTaxRules();
  const pricing = rules?.monetization;

  const plans = [
    {
      name: 'Free estimate',
      price: '₦0',
      cadence: 'always free',
      description: 'For anyone who wants to know where they stand.',
      features: ['Estimated tax payable', 'Taxable income & effective rate', 'VAT threshold check', 'Key filing deadlines', 'Ask TaxNavi questions'],
      cta: { href: '/calculate', label: 'Calculate my tax', variant: 'secondary' as const },
    },
    {
      name: 'Full tax report',
      price: pricing ? formatCurrency(pricing.reportPrices?.Individual ?? pricing.payPerReportPrice) : null,
      cadence: 'one-time, per report',
      description: pricing
        ? `Personal reports. Business (SME) reports are ${formatCurrency(pricing.reportPrices?.SME ?? pricing.payPerReportPrice)}. No account needed.`
        : 'For employees, freelancers and businesses preparing to file.',
      features: ['Band-by-band tax breakdown', 'Deduction analysis', 'Personalised recommendations', 'Compliance checks', 'Downloadable PDF report'],
      cta: { href: '/calculate', label: 'Start with a free estimate', variant: 'primary' as const },
      featured: true,
    },
    {
      name: 'SME plan',
      price: pricing ? formatCurrency(pricing.smeSubscriptionPrice) : null,
      cadence: 'per month',
      description: 'For small businesses without an in-house accountant.',
      features: ['Unlimited full business reports', 'Company tax & VAT checks', 'Compliance tracker', 'Tax calendar & document checklist', 'Saved history for every report'],
      cta: { href: '/auth/register?type=SME', label: 'Create a business account', variant: 'secondary' as const },
    },
  ];

  return (
    <div className="grid gap-5 lg:grid-cols-3">
      {plans.map((plan) => (
        <div key={plan.name} className={`card flex flex-col p-6 ${plan.featured ? 'ring-2 ring-brand-700' : ''}`}>
          <div className="flex items-center justify-between gap-2">
            <h3 className="text-base font-semibold">{plan.name}</h3>
            {plan.featured && <Badge tone="brand">Most popular</Badge>}
          </div>
          <p className="mt-1 text-sm text-ink-subtle">{plan.description}</p>
          <div className="mt-5 flex items-baseline gap-1.5">
            {plan.price ? <span className="figure text-3xl font-semibold">{plan.price}</span> : <Skeleton className="h-9 w-24" />}
            <span className="text-sm text-ink-subtle">{plan.cadence}</span>
          </div>
          <ul className="mt-6 flex-1 space-y-2.5">
            {plan.features.map((feature) => (
              <li key={feature} className="flex gap-2.5 text-sm">
                <Check size={17} className="mt-0.5 shrink-0 text-brand-700" />
                {feature}
              </li>
            ))}
          </ul>
          <ButtonLink href={plan.cta.href} variant={plan.cta.variant} block className="mt-7">
            {plan.cta.label}
          </ButtonLink>
        </div>
      ))}
    </div>
  );
}
