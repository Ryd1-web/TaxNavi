export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  userType: 'Individual' | 'SME';
}

export interface AuthResponse {
  success: boolean;
  message: string;
  jwtToken?: string;
  refreshToken?: string;
  user?: User;
}

export interface TaxReport {
  id: string;
  userId: string;
  reportType: string;
  taxLiability: number;
  totalIncome: number;
  allowableDeductions: number;
  projectedSavings: number;
  pdfUrl: string;
  createdAt: string;
}

export interface IndividualTaxData {
  userId: string;
  monthlySalary: number;
  annualBonus: number;
  allowances: number;
  businessIncome: number;
  freelanceIncome: number;
  investmentIncome: number;
  rentPaid: number;
  loanInterest: number;
  otherExpenses: number;
}

export interface SMETaxData {
  userId: string;
  businessName: string;
  businessType: string;
  annualTurnover: number;
  operatingExpenses: number;
  payrollCost: number;
  rentAndUtilities: number;
  assets: number;
  vatableIncome: number;
}

export interface TaxCalculationResult {
  reportId?: string;
  taxLiability: number;
  totalIncome: number;
  allowableDeductions: number;
  consolidatedRelief?: number;
  projectedSavings: number;
  taxBands?: Record<string, number>;
  aiSummary?: string;
  savingsAdvice?: string;
}

export interface SMETaxCalculationResult {
  vatEligible: boolean;
  vatLiability: number;
  whtLiability: number;
  totalTaxObligation: number;
  complianceRoadmap: string;
}

export interface PaymentInitiationResponse {
  reference: string;
  authorizationUrl: string;
  accessCode: string;
}
