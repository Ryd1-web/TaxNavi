import Link from 'next/link';
import PublicLayout from '@/components/layout/PublicLayout';
import PricingSection from '@/components/marketing/PricingSection';
import BandBreakdown from '@/components/tax/BandBreakdown';
import IncomeBreakdownBar from '@/components/tax/IncomeBreakdownBar';
import { ButtonLink } from '@/components/ui/Button';
import {
  ArrowRight,
  BarChart,
  Briefcase,
  Building,
  Calculator,
  Calendar,
  Check,
  ClipboardCheck,
  FileText,
  Lock,
  MessageCircle,
  Phone,
  Scale,
  Shield,
  UserIcon,
} from '@/components/ui/Icons';
import { Badge } from '@/components/ui/Primitives';

// Worked example under the Nigeria Tax Act 2025 — the same case is asserted in the backend test suite
// (₦5m salary, ₦1.2m rent, ₦400k pension → ₦574,800).
const example = {
  gross: 5_000_000,
  taxable: 4_360_000,
  tax: 574_800,
  bands: [
    { label: 'b1', lowerLimit: 0, upperLimit: 800_000, taxableAmount: 800_000, rate: 0, taxAmount: 0 },
    { label: 'b2', lowerLimit: 800_000, upperLimit: 3_000_000, taxableAmount: 2_200_000, rate: 0.15, taxAmount: 330_000 },
    { label: 'b3', lowerLimit: 3_000_000, upperLimit: 12_000_000, taxableAmount: 1_360_000, rate: 0.18, taxAmount: 244_800 },
  ],
};

const steps = [
  { icon: <UserIcon />, title: 'Tell us about you', body: 'Employee, freelancer or business owner — we only ask what applies to you.' },
  { icon: <Calculator />, title: 'Enter your figures', body: 'Income, expenses and deductions, in plain language with examples. No forms to decode.' },
  { icon: <BarChart />, title: 'See your tax clearly', body: 'Your estimated tax, effective rate, VAT position and next steps — in about three minutes.' },
];

const features = [
  { icon: <Scale />, title: 'Up to date with the 2025 reforms', body: 'Built on the Nigeria Tax Act 2025 from 1 January 2026, with the old rules still available for 2025 returns.' },
  { icon: <FileText />, title: 'Transparent calculations', body: 'Every figure shows its working: bands, reliefs, deductions and the assumptions behind them.' },
  { icon: <Check />, title: 'Deductions you might miss', body: 'Rent relief, pension, NHF, NHIS, life assurance and mortgage interest — checked automatically.' },
  { icon: <Building />, title: 'VAT & company tax checks', body: 'Know whether your business is a small company, and when VAT registration applies.' },
  { icon: <Calendar />, title: 'Deadlines that matter to you', body: 'PAYE, VAT and annual return dates based on your situation, not a generic list.' },
  { icon: <MessageCircle />, title: 'Answers in plain English', body: 'Ask TaxNavi explains your result using the same rules as the calculator. It never guesses.' },
];

const faqs = [
  {
    q: 'Which tax rules does TaxNavi use?',
    a: 'From tax year 2026 we apply the Nigeria Tax Act 2025, which took effect on 1 January 2026. For 2024 and 2025 we apply the previous Personal Income Tax Act rules, because those years are still assessed under the old law. Every result shows the rule set and tax year used.',
  },
  {
    q: 'Do I need an account to calculate my tax?',
    a: 'No. The free estimate works without signing up, and your answers stay on your device. You only need an account to save reports or unlock the full report, so that only you can open it.',
  },
  {
    q: 'How accurate is the estimate?',
    a: 'Calculations are deterministic and follow the published rates, bands and reliefs. Where official guidance is still inconsistent — for example some VAT and company-size thresholds — we flag the figure as "pending validation" rather than present it as settled. Your result is only as accurate as the figures you enter.',
  },
  {
    q: 'Is this tax advice?',
    a: 'No. TaxNavi gives you an informed estimate and explains the rules. For disputes, penalties, foreign income, capital gains or complex structures, speak to a chartered tax practitioner or your tax authority.',
  },
  {
    q: 'What happened to the Consolidated Relief Allowance (CRA)?',
    a: 'Under the Nigeria Tax Act 2025 the CRA no longer applies. Instead, the first ₦800,000 of taxable income is taxed at 0%, and you can claim rent relief of 20% of the rent you pay on your home, up to ₦500,000.',
  },
  {
    q: 'How is my data protected?',
    a: 'Anonymous calculations are not stored on our servers. Saved reports are tied to your account and every request is checked on the server, so no one else can open them. Payments are handled by Paystack; we never see your card details.',
  },
];

export default function HomePage() {
  return (
    <PublicLayout>
      {/* Hero */}
      <section className="relative overflow-hidden border-b border-line bg-surface">
        <div className="absolute inset-0 -z-0 hero-glow" aria-hidden="true" />
        <div className="container-page relative grid grid-cols-[minmax(0,1fr)] items-center gap-12 py-14 sm:py-20 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)] lg:py-24">
          <div>
            <Badge tone="brand">Updated for the Nigeria Tax Act 2025</Badge>
            <h1 className="mt-5 text-[40px] font-semibold leading-[1.05] tracking-tight sm:text-[52px] lg:text-[58px]">
              Understand your taxes. <span className="text-brand-700">Keep more of what you earn.</span>
            </h1>
            <p className="mt-5 max-w-xl text-lg leading-8 text-ink-muted">
              Calculate your Nigerian taxes, discover eligible deductions and stay compliant — all in one intelligent platform.
            </p>
            <div className="mt-8 flex flex-col gap-3 sm:flex-row">
              <ButtonLink href="/calculate" size="lg">
                Calculate my tax <ArrowRight size={17} />
              </ButtonLink>
              <ButtonLink href="#how-it-works" size="lg" variant="secondary">
                Explore TaxNavi
              </ButtonLink>
            </div>
            <ul className="mt-8 flex flex-wrap gap-x-6 gap-y-2 text-sm text-ink-muted">
              {['Free estimate', 'No sign-up needed', 'About 3 minutes'].map((item) => (
                <li key={item} className="flex items-center gap-1.5">
                  <Check size={16} className="text-brand-700" />
                  {item}
                </li>
              ))}
            </ul>
          </div>

          <div className="card relative p-6 shadow-raised sm:p-7" aria-label="Example result">
            <div className="flex items-center justify-between">
              <Badge tone="neutral">Example · Tax year 2026</Badge>
              <span className="text-xs text-ink-subtle">Salaried employee</span>
            </div>
            <p className="mt-6 text-sm text-ink-subtle">Estimated tax payable</p>
            <p className="figure mt-1 text-[44px] font-semibold leading-none">₦574,800</p>
            <p className="mt-2 text-sm text-ink-muted">
              About <span className="figure font-medium text-ink">₦47,900</span> a month · effective rate 11.5%
            </p>
            <div className="mt-6 border-t border-line pt-5">
              <IncomeBreakdownBar grossIncome={example.gross} taxableIncome={example.taxable} taxPayable={example.tax} />
            </div>
            <p className="mt-5 rounded-lg bg-surface-subtle px-3 py-2 text-xs leading-5 text-ink-subtle">
              ₦5,000,000 salary, ₦1,200,000 rent and ₦400,000 pension. Rent relief and pension reduce taxable income to ₦4,360,000.
            </p>
          </div>
        </div>
      </section>

      {/* How it works */}
      <section id="how-it-works" className="scroll-mt-20 py-16 sm:py-24">
        <div className="container-page">
          <SectionIntro eyebrow="How it works" title="From payslip to clarity in three steps" body="A guided flow that feels like a conversation, not a tax form." />
          <ol className="mt-12 grid gap-5 md:grid-cols-3">
            {steps.map((step, index) => (
              <li key={step.title} className="card p-6">
                <div className="flex items-center gap-3">
                  <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-50 text-brand-700">{step.icon}</span>
                  <span className="figure text-sm font-semibold text-ink-subtle">0{index + 1}</span>
                </div>
                <h3 className="mt-5 text-lg font-semibold">{step.title}</h3>
                <p className="mt-2 text-[15px] leading-6 text-ink-muted">{step.body}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {/* Features */}
      <section className="border-y border-line bg-surface py-16 sm:py-24">
        <div className="container-page">
          <SectionIntro eyebrow="Why TaxNavi" title="Built for how Nigerians actually earn" body="Salaries, side hustles, freelance work and small businesses — in one place." />
          <div className="mt-12 grid gap-x-10 gap-y-10 sm:grid-cols-2 lg:grid-cols-3">
            {features.map((feature) => (
              <div key={feature.title}>
                <span className="flex h-10 w-10 items-center justify-center rounded-xl border border-line text-brand-700">{feature.icon}</span>
                <h3 className="mt-4 text-base font-semibold">{feature.title}</h3>
                <p className="mt-1.5 text-[15px] leading-6 text-ink-muted">{feature.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Solutions */}
      <section id="solutions" className="scroll-mt-20 py-16 sm:py-24">
        <div className="container-page">
          <SectionIntro eyebrow="Personal & SME" title="One platform, two clear paths" />
          <div className="mt-12 grid gap-5 lg:grid-cols-2">
            <SolutionCard
              icon={<Briefcase />}
              title="For employees & freelancers"
              body="Know what you owe on your salary and side income, check your PAYE, and make sure every relief you are entitled to is claimed."
              points={['PAYE and annual tax estimate', 'Rent relief and statutory deductions', 'Freelance profit after expenses', 'Your personal filing deadlines']}
              href="/calculate?type=individual"
              cta="Calculate personal tax"
            />
            <SolutionCard
              icon={<Building />}
              title="For small businesses"
              body="A plain-language view of your obligations — without hiring a full-time accountant."
              points={['Small-company status and company tax', 'VAT registration check', 'Compliance tracker and document checklist', 'Tax calendar for PAYE, VAT and returns']}
              href="/calculate?type=sme"
              cta="Check business tax"
            />
          </div>
        </div>
      </section>

      {/* Report preview */}
      <section className="border-y border-line bg-surface py-16 sm:py-24">
        <div className="container-page grid grid-cols-[minmax(0,1fr)] items-center gap-12 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
          <div>
            <p className="eyebrow">Full tax report</p>
            <h2 className="mt-2 text-3xl font-semibold sm:text-4xl">See exactly how your tax is worked out</h2>
            <p className="mt-4 text-[15px] leading-7 text-ink-muted">
              The full report shows each band, every deduction you claimed against what the law allows, and practical steps to avoid overpaying — ready to download as a PDF for your records or your accountant.
            </p>
            <ul className="mt-6 space-y-3">
              {['Band-by-band breakdown', 'Deduction analysis', 'Personalised recommendations', 'Compliance points before you file'].map((item) => (
                <li key={item} className="flex gap-2.5 text-[15px]">
                  <Check size={18} className="mt-0.5 shrink-0 text-brand-700" />
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="card p-5 shadow-raised sm:p-6" aria-label="Example full report">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-semibold">How each naira is taxed</p>
                <p className="text-[13px] text-ink-subtle">Example · ₦4,360,000 taxable income</p>
              </div>
              <Badge tone="gold">Full report</Badge>
            </div>
            <div className="mt-5">
              <BandBreakdown bands={example.bands} />
            </div>
            <div className="mt-5 flex items-center justify-between rounded-xl bg-surface-subtle px-4 py-3 text-sm">
              <span className="text-ink-muted">Total estimated tax</span>
              <span className="figure font-semibold">₦574,800</span>
            </div>
          </div>
        </div>
      </section>

      {/* Pricing */}
      <section id="pricing" className="scroll-mt-20 py-16 sm:py-24">
        <div className="container-page">
          <SectionIntro eyebrow="Pricing" title="Start free. Pay only for what you need." body="No subscription required for individuals. All prices in naira." />
          <div className="mt-12">
            <PricingSection />
          </div>
        </div>
      </section>

      {/* Trust */}
      <section id="trust" className="scroll-mt-20 border-y border-line bg-night py-16 text-white sm:py-24">
        <div className="container-page">
          <div className="max-w-2xl">
            <p className="text-xs font-semibold uppercase tracking-[0.08em] text-gold-300">Trust & security</p>
            <h2 className="mt-2 text-3xl font-semibold text-white sm:text-4xl">Serious about accuracy. Careful with your data.</h2>
          </div>
          <div className="mt-12 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
            {[
              { icon: <Scale />, title: 'Rules, not guesses', body: 'Every calculation comes from a versioned rule set for the tax year. Unconfirmed figures are flagged.' },
              { icon: <Lock />, title: 'Private by default', body: 'Anonymous calculations never leave your device. We only store what you choose to save.' },
              { icon: <Shield />, title: 'Your reports are yours', body: 'Access to every report and payment is checked on our servers, not just in your browser.' },
              { icon: <Phone />, title: 'Made for mobile', body: 'Designed for the phone in your pocket — fast, clear and easy to use anywhere.' },
            ].map((item) => (
              <div key={item.title}>
                <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#fff]/10 text-gold-300">{item.icon}</span>
                <h3 className="mt-4 text-base font-semibold text-white">{item.title}</h3>
                <p className="mt-1.5 text-[15px] leading-6 text-slate-300">{item.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* FAQ */}
      <section id="faq" className="scroll-mt-20 py-16 sm:py-24">
        <div className="container-page grid grid-cols-[minmax(0,1fr)] gap-10 lg:grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)]">
          <SectionIntro eyebrow="FAQ" title="Questions, answered" body="Can't find what you need? Ask TaxNavi after your calculation." align="left" />
          <div className="divide-y divide-line border-y border-line">
            {faqs.map((faq) => (
              <details key={faq.q} className="group py-1">
                <summary className="flex min-h-[56px] cursor-pointer list-none items-center justify-between gap-4 rounded-lg py-3 text-left text-[15px] font-semibold">
                  {faq.q}
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-line text-ink-subtle transition-transform group-open:rotate-45" aria-hidden="true">
                    +
                  </span>
                </summary>
                <p className="pb-5 pr-10 text-[15px] leading-7 text-ink-muted">{faq.a}</p>
              </details>
            ))}
          </div>
        </div>
      </section>

      {/* Final CTA */}
      <section className="pb-20">
        <div className="container-page">
          <div className="card flex flex-col items-start justify-between gap-6 bg-brand-900 p-8 text-white sm:p-10 md:flex-row md:items-center">
            <div>
              <h2 className="text-2xl font-semibold text-white sm:text-3xl">Know your number before the taxman does.</h2>
              <p className="mt-2 text-brand-100">Free, private and done in about three minutes.</p>
            </div>
            <Link href="/calculate" className="btn btn-lg bg-[#fff] text-[#083f31] hover:bg-[#ecf8f3]">
              Calculate my tax <ArrowRight size={17} />
            </Link>
          </div>
        </div>
      </section>
    </PublicLayout>
  );
}

function SectionIntro({ eyebrow, title, body, align = 'center' }: { eyebrow: string; title: string; body?: string; align?: 'center' | 'left' }) {
  return (
    <div className={align === 'center' ? 'mx-auto max-w-2xl text-center' : 'max-w-md'}>
      <p className="eyebrow">{eyebrow}</p>
      <h2 className="mt-2 text-3xl font-semibold sm:text-4xl">{title}</h2>
      {body && <p className="mt-3 text-[15px] leading-7 text-ink-muted">{body}</p>}
    </div>
  );
}

function SolutionCard({ icon, title, body, points, href, cta }: { icon: React.ReactNode; title: string; body: string; points: string[]; href: string; cta: string }) {
  return (
    <div className="card flex flex-col p-6 sm:p-8">
      <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-700 text-white">{icon}</span>
      <h3 className="mt-5 text-xl font-semibold">{title}</h3>
      <p className="mt-2 text-[15px] leading-6 text-ink-muted">{body}</p>
      <ul className="mt-6 flex-1 space-y-2.5">
        {points.map((point) => (
          <li key={point} className="flex gap-2.5 text-sm">
            <ClipboardCheck size={17} className="mt-0.5 shrink-0 text-brand-700" />
            {point}
          </li>
        ))}
      </ul>
      <ButtonLink href={href} variant="secondary" className="mt-7 self-start">
        {cta} <ArrowRight size={16} />
      </ButtonLink>
    </div>
  );
}
