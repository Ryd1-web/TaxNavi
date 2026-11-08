import { BusinessStructure, ExpenseItem, IncomeSource, TaxCalculationRequest } from '@/types';

export type Category = '' | 'employee' | 'self-employed' | 'business';
export type SourceKey = 'salary' | 'freelance' | 'business' | 'investment' | 'rental' | 'other';

export interface CalculatorState {
  taxYear: number;
  category: Category;
  sources: Record<SourceKey, boolean>;

  salary: number;
  salaryPeriod: 'monthly' | 'annual';
  bonus: number;
  allowances: number;
  freelanceIncome: number;
  businessIncome: number;
  investmentIncome: number;
  investmentFinalTax: boolean;
  rentalIncome: number;
  otherIncome: number;

  businessName: string;
  businessType: string;
  businessStructure: BusinessStructure;
  turnover: number;
  fixedAssets: number;
  isProfessionalServices: boolean;

  costOfSales: number;
  operatingExpenses: number;
  payroll: number;
  rentAndUtilities: number;
  professionalFees: number;
  otherExpenses: number;

  pension: number;
  nhf: number;
  nhis: number;
  lifeAssurance: number;
  mortgageInterest: number;
  annualRent: number;
}

export type StepId = 'category' | 'sources' | 'business' | 'income' | 'expenses' | 'deductions' | 'review';

export const STEP_TITLES: Record<StepId, string> = {
  category: 'About you',
  sources: 'Income sources',
  business: 'Your business',
  income: 'Income',
  expenses: 'Expenses',
  deductions: 'Deductions',
  review: 'Review',
};

export function initialState(taxYear: number, category: Category = ''): CalculatorState {
  return {
    taxYear,
    category,
    sources: { salary: category === 'employee', freelance: category === 'self-employed', business: false, investment: false, rental: false, other: false },
    salary: 0,
    salaryPeriod: 'monthly',
    bonus: 0,
    allowances: 0,
    freelanceIncome: 0,
    businessIncome: 0,
    investmentIncome: 0,
    investmentFinalTax: true,
    rentalIncome: 0,
    otherIncome: 0,
    businessName: '',
    businessType: '',
    businessStructure: '',
    turnover: 0,
    fixedAssets: 0,
    isProfessionalServices: false,
    costOfSales: 0,
    operatingExpenses: 0,
    payroll: 0,
    rentAndUtilities: 0,
    professionalFees: 0,
    otherExpenses: 0,
    pension: 0,
    nhf: 0,
    nhis: 0,
    lifeAssurance: 0,
    mortgageInterest: 0,
    annualRent: 0,
  };
}

export const isCompany = (s: CalculatorState) => s.category === 'business' && s.businessStructure === 'LimitedCompany';
export const hasBusinessActivity = (s: CalculatorState) =>
  s.category === 'business' || (s.sources.freelance && s.freelanceIncome > 0) || (s.sources.business && s.businessIncome > 0);

/** Only the steps that are relevant to this taxpayer are shown. */
export function stepsFor(state: CalculatorState): StepId[] {
  if (state.category === 'business') {
    return isCompany(state) ? ['category', 'business', 'income', 'expenses', 'review'] : ['category', 'business', 'income', 'expenses', 'deductions', 'review'];
  }
  const steps: StepId[] = ['category', 'sources', 'income'];
  if (state.sources.freelance || state.sources.business) steps.push('expenses');
  steps.push('deductions', 'review');
  return steps;
}

export function annualSalary(state: CalculatorState) {
  return state.salaryPeriod === 'monthly' ? state.salary * 12 : state.salary;
}

export type StepErrors = Partial<Record<string, string>>;

export function validateStep(step: StepId, state: CalculatorState): StepErrors {
  const errors: StepErrors = {};

  if (step === 'category' && !state.category) {
    errors.category = 'Choose the option that best describes you.';
  }

  if (step === 'sources' && !Object.values(state.sources).some(Boolean)) {
    errors.sources = 'Select at least one source of income.';
  }

  if (step === 'business') {
    if (!state.businessStructure) errors.businessStructure = 'Choose how your business is registered.';
    if (!state.businessType.trim()) errors.businessType = 'Tell us what your business does, e.g. retail, logistics or software.';
  }

  if (step === 'income') {
    if (state.category === 'business') {
      if (state.turnover <= 0) errors.turnover = 'Enter your expected annual turnover (total sales before expenses).';
    } else {
      const required: [SourceKey, number, string][] = [
        ['salary', state.salary, 'salary'],
        ['freelance', state.freelanceIncome, 'freelanceIncome'],
        ['business', state.businessIncome, 'businessIncome'],
        ['investment', state.investmentIncome, 'investmentIncome'],
        ['rental', state.rentalIncome, 'rentalIncome'],
        ['other', state.otherIncome, 'otherIncome'],
      ];
      required.forEach(([source, amount, field]) => {
        if (state.sources[source] && amount <= 0) errors[field] = 'Enter an amount, or go back and untick this income source.';
      });
    }
  }

  if (step === 'deductions') {
    const salary = annualSalary(state);
    if (state.sources.salary && salary > 0 && state.pension > salary) {
      errors.pension = 'Pension contributions look higher than your annual salary — please check the amount.';
    }
  }

  return errors;
}

export function buildRequest(state: CalculatorState): TaxCalculationRequest {
  const incomes: IncomeSource[] = [];
  const expenses: ExpenseItem[] = [];
  const add = (name: string, category: IncomeSource['category'], annualAmount: number, finalTaxDeducted = false) => {
    if (annualAmount > 0) incomes.push({ name, category, annualAmount, finalTaxDeducted });
  };

  if (state.category === 'business') {
    add(state.businessName.trim() || 'Business turnover', 'Business', state.turnover);
    if (state.sources.other) add('Other income', 'Other', state.otherIncome);
  } else {
    if (state.sources.salary) {
      add('Salary', 'Employment', annualSalary(state));
      add('Bonus', 'Bonus', state.bonus);
      add('Allowances', 'Allowance', state.allowances);
    }
    if (state.sources.freelance) add('Freelance income', 'Freelance', state.freelanceIncome);
    if (state.sources.business) add('Business income', 'Business', state.businessIncome);
    if (state.sources.investment) add('Investment income', 'Investment', state.investmentIncome, state.investmentFinalTax);
    if (state.sources.rental) add('Rental income', 'Rental', state.rentalIncome);
    if (state.sources.other) add('Other income', 'Other', state.otherIncome);
  }

  const expense = (name: string, category: ExpenseItem['category'], annualAmount: number) => {
    if (annualAmount > 0) expenses.push({ name, category, annualAmount });
  };
  if (hasBusinessActivity(state) || state.category === 'business') {
    expense('Cost of sales', 'CostOfSales', state.costOfSales);
    expense('Operating expenses', 'Operating', state.operatingExpenses);
    expense('Salaries and wages', 'Payroll', state.payroll);
    expense('Rent and utilities', 'RentAndUtilities', state.rentAndUtilities);
    expense('Professional fees', 'Professional', state.professionalFees);
    expense('Other business expenses', 'Business', state.otherExpenses);
  }

  const taxpayerType =
    state.category === 'business' ? 'SME' : state.sources.freelance || state.sources.business || state.category === 'self-employed' ? 'SelfEmployed' : 'Individual';

  const company = isCompany(state);
  const turnover = state.category === 'business' ? state.turnover : (state.sources.freelance ? state.freelanceIncome : 0) + (state.sources.business ? state.businessIncome : 0);

  return {
    taxYear: state.taxYear,
    taxpayerType,
    businessType: state.businessType.trim(),
    businessStructure: state.category === 'business' ? state.businessStructure : '',
    fixedAssets: company ? state.fixedAssets : 0,
    isProfessionalServices: company && state.isProfessionalServices,
    incomeSources: incomes,
    expenses,
    allowableDeductions: company
      ? { pensionContributions: 0, nhfContributions: 0, nhisContributions: 0, lifeAssurancePremium: 0, mortgageInterest: 0, annualRentPaid: 0 }
      : {
          pensionContributions: state.pension,
          nhfContributions: state.nhf,
          nhisContributions: state.nhis,
          lifeAssurancePremium: state.lifeAssurance,
          mortgageInterest: state.mortgageInterest,
          annualRentPaid: state.annualRent,
        },
    vat: { annualTurnover: turnover },
  };
}

// ---------------------------------------------------------------------------
// Local save & resume. Data never leaves the device unless the user saves a report.
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'taxnavi.calculator.v2';

interface Saved {
  state: CalculatorState;
  step: StepId;
  savedAt: string;
}

export function loadProgress(): Saved | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const saved = JSON.parse(raw) as Saved;
    return saved?.state?.taxYear ? saved : null;
  } catch {
    return null;
  }
}

export function saveProgress(state: CalculatorState, step: StepId) {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ state, step, savedAt: new Date().toISOString() }));
  } catch {
    /* storage full or disabled: progress simply isn't saved */
  }
}

export function clearProgress() {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* ignore */
  }
}
