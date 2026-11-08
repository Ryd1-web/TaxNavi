'use client';

import { forwardRef, ReactNode, useEffect, useRef, useState } from 'react';
import {
  annualSalary,
  CalculatorState,
  Category,
  isCompany,
  SourceKey,
  STEP_TITLES,
  StepErrors,
  StepId,
  stepsFor,
  validateStep,
} from '@/lib/calculator';
import { formatCurrency, formatPercent } from '@/lib/formatters';
import { TaxRules } from '@/types';
import { Button } from '@/components/ui/Button';
import { ChoiceCard, MoneyInput, Select, TextInput, Toggle } from '@/components/ui/Form';
import { ArrowLeft, ArrowRight, Briefcase, Building, Check, Home, Layers, Receipt, UserIcon } from '@/components/ui/Icons';
import { Alert } from '@/components/ui/Primitives';

interface Props {
  state: CalculatorState;
  step: StepId;
  rules: TaxRules | null;
  supportedYears: number[];
  onChange: (patch: Partial<CalculatorState>) => void;
  onStepChange: (step: StepId) => void;
  onSubmit: () => void;
  submitting: boolean;
  error?: string | null;
}

export default function CalculatorWizard({ state, step, rules, supportedYears, onChange, onStepChange, onSubmit, submitting, error }: Props) {
  const steps = stepsFor(state);
  const index = Math.max(0, steps.indexOf(step));
  const [errors, setErrors] = useState<StepErrors>({});
  const headingRef = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);

  // Move focus to the step heading on navigation so screen readers announce the new step.
  useEffect(() => {
    setErrors({});
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    headingRef.current?.focus();
    headingRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }, [step]);

  const goNext = () => {
    const stepErrors = validateStep(step, state);
    setErrors(stepErrors);
    if (Object.keys(stepErrors).length) return;
    if (step === 'review') onSubmit();
    else onStepChange(steps[index + 1]);
  };

  const goBack = () => index > 0 && onStepChange(steps[index - 1]);

  // Clear a field's error as soon as the user changes that field.
  const change = (patch: Partial<CalculatorState>) => {
    setErrors((current) => {
      const next = { ...current };
      Object.keys(patch).forEach((key) => delete next[key]);
      return next;
    });
    onChange(patch);
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[220px_minmax(0,1fr)] lg:gap-10">
      {/* Progress */}
      <nav aria-label="Calculator progress" className="lg:pt-2">
        <div className="lg:hidden">
          <div className="flex items-center justify-between text-[13px] font-medium">
            <span className="text-ink">{STEP_TITLES[step]}</span>
            <span className="text-ink-subtle">
              Step {index + 1} of {steps.length}
            </span>
          </div>
          <div className="mt-2 h-1.5 rounded-full bg-line" role="progressbar" aria-valuemin={1} aria-valuemax={steps.length} aria-valuenow={index + 1} aria-label="Progress">
            <div className="h-1.5 rounded-full bg-brand-600 transition-all" style={{ width: `${((index + 1) / steps.length) * 100}%` }} />
          </div>
        </div>
        <ol className="hidden space-y-1 lg:block">
          {steps.map((id, i) => {
            const done = i < index;
            const current = i === index;
            return (
              <li key={id}>
                <button
                  type="button"
                  disabled={i > index}
                  onClick={() => onStepChange(id)}
                  aria-current={current ? 'step' : undefined}
                  className={`flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left text-sm transition-colors disabled:cursor-default ${current ? 'font-semibold text-ink' : done ? 'text-ink-muted hover:bg-surface' : 'text-ink-subtle'}`}
                >
                  <span
                    className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold ${
                      done ? 'bg-brand-700 text-white' : current ? 'border-2 border-brand-700 text-brand-800' : 'border border-line-strong text-ink-subtle'
                    }`}
                  >
                    {done ? <Check size={13} strokeWidth={3} /> : i + 1}
                  </span>
                  {STEP_TITLES[id]}
                </button>
              </li>
            );
          })}
        </ol>
      </nav>

      <div className="card p-5 sm:p-8">
        <form
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            goNext();
          }}
        >
          <StepHeading ref={headingRef} step={step} state={state} />

          {Object.keys(errors).length > 0 && (
            <Alert tone="error" className="mt-5">
              {Object.values(errors)[0]}
            </Alert>
          )}
          {error && step === 'review' && (
            <Alert tone="error" className="mt-5">
              {error}
            </Alert>
          )}

          <div className="mt-6">
            {step === 'category' && <CategoryStep state={state} onChange={change} supportedYears={supportedYears} rules={rules} />}
            {step === 'sources' && <SourcesStep state={state} onChange={change} />}
            {step === 'business' && <BusinessStep state={state} onChange={change} errors={errors} rules={rules} />}
            {step === 'income' && <IncomeStep state={state} onChange={change} errors={errors} />}
            {step === 'expenses' && <ExpensesStep state={state} onChange={change} />}
            {step === 'deductions' && <DeductionsStep state={state} onChange={change} errors={errors} rules={rules} />}
            {step === 'review' && <ReviewStep state={state} onEdit={onStepChange} />}
          </div>

          <div className="sticky bottom-0 -mx-5 mt-8 flex items-center justify-between gap-3 border-t border-line bg-surface px-5 py-4 sm:static sm:mx-0 sm:px-0 sm:pb-0">
            <Button variant="ghost" onClick={goBack} disabled={index === 0} className={index === 0 ? 'invisible' : ''}>
              <ArrowLeft size={16} /> Back
            </Button>
            <Button type="submit" size="lg" loading={submitting} loadingText="Calculating…">
              {step === 'review' ? 'Calculate my tax' : 'Continue'} <ArrowRight size={16} />
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}

const headings: Record<StepId, { title: string; body: string }> = {
  category: { title: 'Who is this calculation for?', body: 'We will only ask questions that apply to you.' },
  sources: { title: 'How do you earn money?', body: 'Select everything that applies for the year. You can enter amounts next.' },
  business: { title: 'Tell us about your business', body: 'This decides whether personal income tax or company income tax applies.' },
  income: { title: 'How much did you earn?', body: 'Enter amounts for the whole tax year, before tax. Estimates are fine.' },
  expenses: { title: 'Business expenses', body: 'Business income is taxed on profit, so costs of running the business come off first.' },
  deductions: { title: 'Reliefs and deductions', body: 'These reduce the income you are taxed on. Your payslip usually shows them.' },
  review: { title: 'Review your answers', body: 'Check everything looks right. You can edit any section.' },
};

const StepHeading = forwardRef<HTMLHeadingElement, { step: StepId; state: CalculatorState }>(function StepHeading({ step, state }, ref) {
  const heading = headings[step];
  const body = step === 'expenses' && isCompany(state) ? 'Company tax is charged on profit, so allowable running costs are deducted from turnover.' : heading.body;
  return (
    <div>
      <h2 ref={ref} tabIndex={-1} className="text-xl font-semibold outline-none sm:text-2xl">
        {heading.title}
      </h2>
      <p className="mt-1.5 text-[15px] leading-6 text-ink-muted">{body}</p>
    </div>
  );
});

function SegmentedControl<T extends string>({ value, options, onChange, label }: { value: T; options: { value: T; label: string }[]; onChange: (value: T) => void; label: string }) {
  return (
    <div role="radiogroup" aria-label={label} className="inline-flex rounded-lg bg-surface-muted p-1">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={value === option.value}
          onClick={() => onChange(option.value)}
          className={`min-h-[36px] rounded-md px-3 text-[13px] font-medium transition-colors ${value === option.value ? 'bg-surface text-ink shadow-card' : 'text-ink-subtle hover:text-ink'}`}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

type StepProps = { state: CalculatorState; onChange: (patch: Partial<CalculatorState>) => void };

function CategoryStep({ state, onChange, supportedYears, rules }: StepProps & { supportedYears: number[]; rules: TaxRules | null }) {
  const choose = (category: Category) => {
    const sources = { ...state.sources };
    if (category === 'employee' && !Object.values(sources).some(Boolean)) sources.salary = true;
    if (category === 'self-employed' && !sources.freelance && !sources.business) sources.freelance = true;
    onChange({ category, sources });
  };

  const yearLabel = (year: number) => {
    if (rules && year === rules.defaultTaxYear) return `${year} — current year (Nigeria Tax Act 2025)`;
    if (year === 2025) return `${year} — returns due 31 March 2026 (old rules)`;
    return `${year} (old rules)`;
  };

  return (
    <div className="space-y-6">
      <fieldset>
        <legend className="sr-only">Taxpayer type</legend>
        <div className="grid gap-3">
          <ChoiceCard name="category" checked={state.category === 'employee'} onChange={() => choose('employee')} icon={<UserIcon />} title="I earn a salary" description="Employees paid through PAYE, including those with side income." />
          <ChoiceCard name="category" checked={state.category === 'self-employed'} onChange={() => choose('self-employed')} icon={<Briefcase />} title="I'm self-employed or freelance" description="Contractors, consultants, creators, traders and sole proprietors." />
          <ChoiceCard name="category" checked={state.category === 'business'} onChange={() => choose('business')} icon={<Building />} title="I run a registered business" description="SMEs — business names and limited companies. Includes VAT and company tax checks." />
        </div>
      </fieldset>
      {supportedYears.length > 0 && (
        <Select
          label="Tax year"
          value={state.taxYear}
          onChange={(event) => onChange({ taxYear: Number(event.target.value) })}
          options={supportedYears.map((year) => ({ value: year, label: yearLabel(year) }))}
          hint="Tax years run from 1 January to 31 December. New rules apply from 2026."
        />
      )}
    </div>
  );
}

const sourceOptions: { key: SourceKey; title: string; description: string }[] = [
  { key: 'salary', title: 'Salary or wages', description: 'From an employer, including bonuses and allowances.' },
  { key: 'freelance', title: 'Freelance or contract work', description: 'Fees from clients, gigs or consulting.' },
  { key: 'business', title: 'Business I own personally', description: 'Trading or services not run through a company.' },
  { key: 'investment', title: 'Investments', description: 'Dividends, interest or other investment returns.' },
  { key: 'rental', title: 'Rent from property', description: 'Income from letting out a property you own.' },
  { key: 'other', title: 'Other income', description: 'Anything else taxable that is not listed above.' },
];

function SourcesStep({ state, onChange }: StepProps) {
  return (
    <fieldset>
      <legend className="sr-only">Income sources</legend>
      <div className="grid gap-3 sm:grid-cols-2">
        {sourceOptions.map((option) => (
          <ChoiceCard
            key={option.key}
            type="checkbox"
            name={`source-${option.key}`}
            checked={state.sources[option.key]}
            onChange={(checked) => onChange({ sources: { ...state.sources, [option.key]: checked } })}
            title={option.title}
            description={option.description}
          />
        ))}
      </div>
    </fieldset>
  );
}

function BusinessStep({ state, onChange, errors, rules }: StepProps & { errors: StepErrors; rules: TaxRules | null }) {
  const cit = rules?.companyIncomeTax;
  return (
    <div className="space-y-6">
      <div className="grid gap-5 sm:grid-cols-2">
        <TextInput label="Business name" optional value={state.businessName} maxLength={100} onChange={(event) => onChange({ businessName: event.target.value })} placeholder="e.g. Adaeze Foods" />
        <TextInput
          label="What does the business do?"
          value={state.businessType}
          maxLength={100}
          error={errors.businessType}
          onChange={(event) => onChange({ businessType: event.target.value })}
          placeholder="e.g. Retail, logistics, software"
        />
      </div>
      <fieldset>
        <legend className="label">How is it registered?</legend>
        {errors.businessStructure && <p className="error-text mb-2 mt-0">{errors.businessStructure}</p>}
        <div className="grid gap-3 sm:grid-cols-2">
          <ChoiceCard name="structure" checked={state.businessStructure === 'SoleProprietor'} onChange={() => onChange({ businessStructure: 'SoleProprietor' })} icon={<UserIcon />} title="Business name / sole trader" description="Profits are taxed as your personal income." />
          <ChoiceCard name="structure" checked={state.businessStructure === 'LimitedCompany'} onChange={() => onChange({ businessStructure: 'LimitedCompany' })} icon={<Building />} title="Limited company (Ltd)" description="The company pays company income tax on its profits." />
        </div>
      </fieldset>
      {isCompany(state) && (
        <div className="space-y-5 rounded-xl border border-line bg-surface-subtle p-4 sm:p-5">
          <MoneyInput
            label="Value of fixed assets"
            value={state.fixedAssets}
            onChange={(fixedAssets) => onChange({ fixedAssets })}
            hint={cit?.smallCompanyFixedAssetLimit ? `Equipment, vehicles, property. Small companies have assets up to ${formatCurrency(cit.smallCompanyFixedAssetLimit)}.` : 'Equipment, vehicles, property and similar long-term assets.'}
          />
          <Toggle
            label="We provide professional services"
            description="E.g. legal, accounting, consulting, engineering. These firms don't qualify as small companies for tax."
            checked={state.isProfessionalServices}
            onChange={(isProfessionalServices) => onChange({ isProfessionalServices })}
          />
        </div>
      )}
    </div>
  );
}

function IncomeStep({ state, onChange, errors }: StepProps & { errors: StepErrors }) {
  if (state.category === 'business') {
    return (
      <div className="space-y-5">
        <MoneyInput label="Annual turnover" value={state.turnover} onChange={(turnover) => onChange({ turnover })} error={errors.turnover} hint="Total sales or fees for the year, before any expenses. Example: 45m." autoFocus />
        <Toggle label="The business has other income" description="E.g. interest earned or sale of scrap — not your main sales." checked={state.sources.other} onChange={(other) => onChange({ sources: { ...state.sources, other } })} />
        {state.sources.other && <MoneyInput label="Other business income" value={state.otherIncome} onChange={(otherIncome) => onChange({ otherIncome })} />}
      </div>
    );
  }

  return (
    <div className="space-y-7">
      {state.sources.salary && (
        <Group title="Salary" icon={<UserIcon size={16} />}>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-ink-muted">Gross pay before tax and deductions</p>
            <SegmentedControl
              label="Salary period"
              value={state.salaryPeriod}
              onChange={(salaryPeriod) => onChange({ salaryPeriod })}
              options={[
                { value: 'monthly', label: 'Monthly' },
                { value: 'annual', label: 'Yearly' },
              ]}
            />
          </div>
          <MoneyInput
            label={state.salaryPeriod === 'monthly' ? 'Monthly gross salary' : 'Annual gross salary'}
            value={state.salary}
            onChange={(salary) => onChange({ salary })}
            error={errors.salary}
            hint={state.salary > 0 ? `That's ${formatCurrency(annualSalary(state))} a year.` : 'Example: 450,000'}
          />
          <div className="grid gap-5 sm:grid-cols-2">
            <MoneyInput label="Bonuses for the year" optional value={state.bonus} onChange={(bonus) => onChange({ bonus })} />
            <MoneyInput label="Allowances not in salary" optional value={state.allowances} onChange={(allowances) => onChange({ allowances })} hint="Only if not already in the figure above." />
          </div>
        </Group>
      )}
      {state.sources.freelance && (
        <Group title="Freelance or contract work" icon={<Briefcase size={16} />}>
          <MoneyInput label="Total fees received in the year" value={state.freelanceIncome} onChange={(freelanceIncome) => onChange({ freelanceIncome })} error={errors.freelanceIncome} hint="Before expenses. You will add expenses next." />
        </Group>
      )}
      {state.sources.business && (
        <Group title="Your business" icon={<Building size={16} />}>
          <MoneyInput label="Total business receipts" value={state.businessIncome} onChange={(businessIncome) => onChange({ businessIncome })} error={errors.businessIncome} hint="Sales or fees before expenses." />
        </Group>
      )}
      {state.sources.investment && (
        <Group title="Investments" icon={<Layers size={16} />}>
          <MoneyInput label="Investment income" value={state.investmentIncome} onChange={(investmentIncome) => onChange({ investmentIncome })} error={errors.investmentIncome} />
          <Toggle
            label="Withholding tax was already deducted as a final tax"
            description="Common for dividends from Nigerian companies and bank interest. If so, it isn't taxed again. Unsure? Check your dividend or interest statement."
            checked={state.investmentFinalTax}
            onChange={(investmentFinalTax) => onChange({ investmentFinalTax })}
          />
        </Group>
      )}
      {state.sources.rental && (
        <Group title="Rental income" icon={<Home size={16} />}>
          <MoneyInput label="Rent received in the year" value={state.rentalIncome} onChange={(rentalIncome) => onChange({ rentalIncome })} error={errors.rentalIncome} />
        </Group>
      )}
      {state.sources.other && (
        <Group title="Other income" icon={<Receipt size={16} />}>
          <MoneyInput label="Other taxable income" value={state.otherIncome} onChange={(otherIncome) => onChange({ otherIncome })} error={errors.otherIncome} />
        </Group>
      )}
    </div>
  );
}

function ExpensesStep({ state, onChange }: StepProps) {
  return (
    <div className="space-y-5">
      <Alert tone="info">Only include costs that are wholly for the business, and keep receipts. Personal spending is not deductible.</Alert>
      <div className="grid gap-5 sm:grid-cols-2">
        <MoneyInput label="Cost of goods sold" optional value={state.costOfSales} onChange={(costOfSales) => onChange({ costOfSales })} hint="Stock and materials you resold." />
        <MoneyInput label="Operating costs" optional value={state.operatingExpenses} onChange={(operatingExpenses) => onChange({ operatingExpenses })} hint="Data, transport, marketing, supplies." />
        <MoneyInput label="Staff salaries" optional value={state.payroll} onChange={(payroll) => onChange({ payroll })} />
        <MoneyInput label="Office rent and utilities" optional value={state.rentAndUtilities} onChange={(rentAndUtilities) => onChange({ rentAndUtilities })} hint="Business premises only, not your home." />
        <MoneyInput label="Professional fees" optional value={state.professionalFees} onChange={(professionalFees) => onChange({ professionalFees })} hint="Accountant, legal, consultants." />
        <MoneyInput label="Other business expenses" optional value={state.otherExpenses} onChange={(otherExpenses) => onChange({ otherExpenses })} />
      </div>
    </div>
  );
}

function DeductionsStep({ state, onChange, errors, rules }: StepProps & { errors: StepErrors; rules: TaxRules | null }) {
  const rentRelief = rules?.reliefModel === 'RentRelief';
  const relief = rentRelief && rules ? Math.min(state.annualRent * rules.rentReliefRate, rules.rentReliefCap) : 0;

  return (
    <div className="space-y-7">
      <Group title="Statutory contributions" icon={<Receipt size={16} />}>
        <div className="grid gap-5 sm:grid-cols-2">
          <MoneyInput label="Pension contributions (yearly)" optional value={state.pension} onChange={(pension) => onChange({ pension })} error={errors.pension} hint="Your own contribution, as on your payslip × 12." />
          <MoneyInput label="National Housing Fund (NHF)" optional value={state.nhf} onChange={(nhf) => onChange({ nhf })} />
          <MoneyInput label="Health insurance (NHIS)" optional value={state.nhis} onChange={(nhis) => onChange({ nhis })} />
          <MoneyInput label="Life assurance premiums" optional value={state.lifeAssurance} onChange={(lifeAssurance) => onChange({ lifeAssurance })} hint="For you or your spouse." />
        </div>
      </Group>
      <Group title="Your home" icon={<Home size={16} />}>
        <div className="grid gap-5 sm:grid-cols-2">
          <div>
            <MoneyInput
              label="Rent you paid for your home"
              optional
              value={state.annualRent}
              onChange={(annualRent) => onChange({ annualRent })}
              hint={
                rentRelief && rules
                  ? state.annualRent > 0
                    ? `Rent relief: ${formatCurrency(relief)} (${formatPercent(rules.rentReliefRate, 0)} of rent, max ${formatCurrency(rules.rentReliefCap)}).`
                    : `${formatPercent(rules.rentReliefRate, 0)} of yearly rent is deductible, up to ${formatCurrency(rules.rentReliefCap)}.`
                  : `For ${state.taxYear}, rent is covered by the Consolidated Relief Allowance, applied automatically.`
              }
            />
          </div>
          <MoneyInput label="Mortgage interest" optional value={state.mortgageInterest} onChange={(mortgageInterest) => onChange({ mortgageInterest })} hint="Interest only, on a loan for the home you live in." />
        </div>
      </Group>
    </div>
  );
}

function ReviewStep({ state, onEdit }: { state: CalculatorState; onEdit: (step: StepId) => void }) {
  const steps = stepsFor(state);
  const rows: { step: StepId; items: [string, string][] }[] = [];

  rows.push({
    step: 'category',
    items: [
      ['Filing as', state.category === 'employee' ? 'Employee' : state.category === 'self-employed' ? 'Self-employed' : 'Business'],
      ['Tax year', String(state.taxYear)],
    ],
  });

  if (state.category === 'business') {
    rows.push({
      step: 'business',
      items: [
        ['Business', state.businessName || state.businessType],
        ['Structure', state.businessStructure === 'LimitedCompany' ? 'Limited company' : 'Business name / sole trader'],
        ...(isCompany(state) ? ([['Fixed assets', formatCurrency(state.fixedAssets)], ['Professional services', state.isProfessionalServices ? 'Yes' : 'No']] as [string, string][]) : []),
      ],
    });
    rows.push({ step: 'income', items: [['Annual turnover', formatCurrency(state.turnover)], ...(state.sources.other ? ([['Other income', formatCurrency(state.otherIncome)]] as [string, string][]) : [])] });
  } else {
    const income: [string, string][] = [];
    if (state.sources.salary) {
      income.push(['Salary (yearly)', formatCurrency(annualSalary(state))]);
      if (state.bonus) income.push(['Bonuses', formatCurrency(state.bonus)]);
      if (state.allowances) income.push(['Allowances', formatCurrency(state.allowances)]);
    }
    if (state.sources.freelance) income.push(['Freelance fees', formatCurrency(state.freelanceIncome)]);
    if (state.sources.business) income.push(['Business receipts', formatCurrency(state.businessIncome)]);
    if (state.sources.investment) income.push(['Investments', `${formatCurrency(state.investmentIncome)}${state.investmentFinalTax ? ' (taxed at source)' : ''}`]);
    if (state.sources.rental) income.push(['Rental income', formatCurrency(state.rentalIncome)]);
    if (state.sources.other) income.push(['Other', formatCurrency(state.otherIncome)]);
    rows.push({ step: 'income', items: income });
  }

  if (steps.includes('expenses')) {
    const total = state.costOfSales + state.operatingExpenses + state.payroll + state.rentAndUtilities + state.professionalFees + state.otherExpenses;
    rows.push({ step: 'expenses', items: [['Total business expenses', formatCurrency(total)]] });
  }

  if (steps.includes('deductions')) {
    const items: [string, string][] = [];
    if (state.pension) items.push(['Pension', formatCurrency(state.pension)]);
    if (state.nhf) items.push(['NHF', formatCurrency(state.nhf)]);
    if (state.nhis) items.push(['NHIS', formatCurrency(state.nhis)]);
    if (state.lifeAssurance) items.push(['Life assurance', formatCurrency(state.lifeAssurance)]);
    if (state.annualRent) items.push(['Rent paid', formatCurrency(state.annualRent)]);
    if (state.mortgageInterest) items.push(['Mortgage interest', formatCurrency(state.mortgageInterest)]);
    rows.push({ step: 'deductions', items: items.length ? items : [['Deductions', 'None entered']] });
  }

  return (
    <div className="space-y-4">
      {rows.map((row) => (
        <div key={row.step} className="rounded-xl border border-line">
          <div className="flex items-center justify-between border-b border-line px-4 py-2.5">
            <h3 className="text-sm font-semibold">{STEP_TITLES[row.step]}</h3>
            <button type="button" className="text-[13px] font-medium text-brand-700 hover:underline" onClick={() => onEdit(row.step)}>
              Edit<span className="sr-only"> {STEP_TITLES[row.step]}</span>
            </button>
          </div>
          <dl className="divide-y divide-line">
            {row.items.map(([label, value]) => (
              <div key={label} className="flex justify-between gap-4 px-4 py-2.5 text-sm">
                <dt className="text-ink-muted">{label}</dt>
                <dd className="figure text-right font-medium">{value}</dd>
              </div>
            ))}
          </dl>
        </div>
      ))}
      <p className="text-[13px] leading-5 text-ink-subtle">
        Your answers are saved on this device only, so you can come back later. Nothing is stored on our servers unless you save the report to an account.
      </p>
    </div>
  );
}

function Group({ title, icon, children }: { title: string; icon: ReactNode; children: ReactNode }) {
  return (
    <section className="space-y-4">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-ink">
        <span className="text-ink-subtle">{icon}</span>
        {title}
      </h3>
      {children}
    </section>
  );
}
