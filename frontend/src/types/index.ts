export type AccountType = 'Individual' | 'SME';

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  userType: AccountType;
}

export type TaxpayerType = 'Individual' | 'SelfEmployed' | 'SME';
export type BusinessStructure = '' | 'SoleProprietor' | 'LimitedCompany';
export type IncomeCategory = 'Employment' | 'Bonus' | 'Allowance' | 'Business' | 'Freelance' | 'Investment' | 'Rental' | 'Other';
export type ExpenseCategory = 'Business' | 'Operating' | 'CostOfSales' | 'Payroll' | 'RentAndUtilities' | 'Professional' | 'Other';
export type VerificationStatus = 'Verified' | 'SecondarySourcesVerified' | 'PendingValidation';

export interface IncomeSource {
  name: string;
  category: IncomeCategory;
  annualAmount: number;
  finalTaxDeducted?: boolean;
}

export interface ExpenseItem {
  name: string;
  category: ExpenseCategory;
  annualAmount: number;
}

export interface AllowableDeductions {
  pensionContributions: number;
  nhfContributions: number;
  nhisContributions: number;
  lifeAssurancePremium: number;
  mortgageInterest: number;
  annualRentPaid: number;
}

export interface TaxCalculationRequest {
  taxYear?: number;
  taxpayerType: TaxpayerType;
  businessType: string;
  businessStructure: BusinessStructure;
  fixedAssets: number;
  isProfessionalServices: boolean;
  incomeSources: IncomeSource[];
  expenses: ExpenseItem[];
  allowableDeductions: AllowableDeductions;
  vat: { annualTurnover: number };
}

export interface TaxSource {
  title: string;
  url: string;
}

export interface RuleSetSummary {
  id: string;
  name: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  legalBasis: string;
  verificationStatus: VerificationStatus;
  sources: TaxSource[];
}

export interface VatAssessment {
  applicable: boolean;
  annualTurnover: number;
  threshold: number;
  vatRate: number;
  vatRequired: boolean;
  statusMessage: string;
  verificationStatus: VerificationStatus;
}

export interface FreeTaxSummary {
  taxYear: number;
  taxType: 'PersonalIncomeTax' | 'CompanyIncomeTax';
  taxpayerType: TaxpayerType;
  estimatedTaxPayable: number;
  grossIncome: number;
  totalDeductionsAndReliefs: number;
  basicTaxableIncome: number;
  effectiveTaxRate: number;
  estimatedMonthlyPaye?: number | null;
  companyClassification?: string | null;
  optimisationOpportunityCount: number;
  complianceFlagCount: number;
  teaserInsights: string[];
  nextSteps: string[];
  vat: VatAssessment;
  applicableRegime: string;
  ruleSet: RuleSetSummary;
  assumptions: string[];
}

export interface TaxBandCharge {
  label: string;
  lowerLimit: number;
  upperLimit?: number | null;
  taxableAmount: number;
  rate: number;
  taxAmount: number;
}

export interface CalculationStep {
  label: string;
  amount: number;
  description: string;
}

export interface DeductionLine {
  label: string;
  claimed: number;
  allowed: number;
  note: string;
}

export interface CompanyTaxAssessment {
  classification: string;
  turnover: number;
  fixedAssets: number;
  allowableExpenses: number;
  assessableProfit: number;
  incomeTaxRate: number;
  incomeTax: number;
  levyName: string;
  levyRate: number;
  levy: number;
  verificationStatus: VerificationStatus;
}

export interface FullTaxReport {
  taxYear: number;
  taxType: 'PersonalIncomeTax' | 'CompanyIncomeTax';
  taxpayerType: TaxpayerType;
  businessType: string;
  grossIncome: number;
  adjustedGrossIncome: number;
  employmentIncome: number;
  selfEmployedIncome: number;
  selfEmployedExpenses: number;
  investmentIncome: number;
  otherIncome: number;
  incomeExcludedAsFinalTax: number;
  consolidatedReliefAllowance: number;
  rentRelief: number;
  deductionAnalysis: DeductionLine[];
  totalAllowableDeductions: number;
  totalDeductionsAndReliefs: number;
  taxableIncome: number;
  taxPayable: number;
  effectiveTaxRate: number;
  estimatedMonthlyPaye?: number | null;
  companyTax?: CompanyTaxAssessment | null;
  vat: VatAssessment;
  taxBandBreakdown: TaxBandCharge[];
  calculationSteps: CalculationStep[];
  optimizationTips: string[];
  complianceWarnings: string[];
  nextSteps: string[];
  assumptions: string[];
  ruleSet: RuleSetSummary;
  savedReportId?: string | null;
  canDownloadPdf: boolean;
  applicableRegime: string;
}

export interface PremiumGate {
  headline: string;
  message: string;
  paymentAmount: number;
  subscriptionAmount: number;
  currency: string;
  lockedFeatures: string[];
  requiresAccount: boolean;
  paymentsEnabled: boolean;
}

export interface TaxReportAccessResponse {
  reportId?: string | null;
  accessGranted: boolean;
  accessLevel: 'Free' | 'Full';
  unlockedVia?: 'Purchase' | 'Subscription' | null;
  freeSummary: FreeTaxSummary;
  fullReport?: FullTaxReport | null;
  premiumGate: PremiumGate;
}

export interface TaxReportListItem {
  id: string;
  reportType: TaxpayerType;
  taxYear: number;
  ruleSetId: string;
  totalIncome: number;
  allowableDeductions: number;
  taxLiability: number;
  isUnlocked: boolean;
  createdAt: string;
}

export interface ComplianceDeadline {
  id: string;
  title: string;
  description: string;
  frequency: 'Monthly' | 'Annual';
  dayOfMonth: number;
  month?: number | null;
  appliesTo: string[];
  authority: string;
  verificationStatus: VerificationStatus;
}

export interface TaxRules {
  defaultTaxYear: number;
  supportedTaxYears: number[];
  taxYear: number;
  ruleSet: RuleSetSummary;
  reliefModel: 'CRA' | 'RentRelief';
  rentReliefRate: number;
  rentReliefCap: number;
  bands: TaxBandCharge[];
  vat: { registrationThreshold: number; rate: number; exemptAtThreshold: boolean; verificationStatus: VerificationStatus; note: string };
  companyIncomeTax: {
    smallCompanyTurnoverLimit: number;
    smallCompanyFixedAssetLimit?: number | null;
    smallCompanyExcludesProfessionalServices: boolean;
    standardRate: number;
    levyName: string;
    levyRate: number;
    verificationStatus: VerificationStatus;
  };
  notes: string[];
  deadlines: ComplianceDeadline[];
  monetization: Pricing;
}

export interface Pricing {
  payPerReportPrice: number;
  /** One-off report price per report type (Individual, SelfEmployed, SME). Server-defined. */
  reportPrices: Record<string, number>;
  smeSubscriptionPrice: number;
  smeSubscriptionPeriodDays: number;
  subscriptionGraceDays: number;
  currency: string;
}

export type SubscriptionStatus = 'Active' | 'NonRenewing' | 'PastDue' | 'Cancelled' | 'Expired';

export interface Entitlements {
  paymentsEnabled: boolean;
  testMode: boolean;
  subscriptionMode: 'Recurring' | 'OneTime';
  hasActiveSubscription: boolean;
  subscriptionPlan?: string | null;
  subscriptionStatus?: SubscriptionStatus | null;
  subscriptionRecurring: boolean;
  subscriptionPeriodEnd?: string | null;
  nextPaymentDate?: string | null;
  canCancelSubscription: boolean;
  unlockedReportCount: number;
  pricing: Pricing;
}

export interface PaymentsConfig {
  enabled: boolean;
  testMode: boolean;
  currency: string;
  provider: string;
}

export interface CheckoutResult {
  reference: string;
  authorizationUrl: string;
  amount: number;
  currency: string;
  taxReportId?: string | null;
  /** Issued once for a new guest report; must be kept on the device to reopen the report. */
  accessToken?: string | null;
  reused: boolean;
}

export interface PaymentRecord {
  reference: string;
  purpose: 'PremiumReport' | 'SmeSubscription';
  status: 'Pending' | 'Completed' | 'Failed' | 'Cancelled' | 'Expired';
  amount: number;
  currency: string;
  taxReportId?: string | null;
  isGuest: boolean;
  createdAt: string;
  paidAt?: string | null;
}

export interface AssistantAnswer {
  topic: string;
  answer: string;
  requiresProfessionalAdvice: boolean;
  grounding: string;
  verificationStatus: VerificationStatus;
  sources: TaxSource[];
  suggestedQuestions: string[];
}

// ----- AI assistant (RAG) -----

export interface AiCitation {
  reference: string;
  documentTitle: string;
  documentType: 'Legislation' | 'Guidance';
  sectionNumber?: string | null;
  sectionTitle?: string | null;
  chapter?: string | null;
  part?: string | null;
  pageFrom: number;
  pageTo: number;
  sourceName: string;
  sourceUrl: string;
  similarity: number;
  snippet: string;
}

export type AiGrounding = 'retrieval' | 'rules' | 'escalation' | 'insufficient';

export interface AiChatResponse {
  conversationId: string;
  guestToken?: string | null;
  answer: string;
  citations: AiCitation[];
  grounding: AiGrounding;
  confidence: 'high' | 'medium' | 'low';
  requiresProfessionalAdvice: boolean;
  disclaimer: string;
  suggestedQuestions: string[];
}

export interface AiChatMessage {
  role: 'user' | 'assistant';
  content: string;
  citations: AiCitation[];
  grounding?: AiGrounding | null;
  createdAt: string;
}

export interface AiConversationSummary {
  id: string;
  title: string;
  createdAt: string;
  updatedAt?: string | null;
}

export interface AiConversation {
  id: string;
  title: string;
  createdAt: string;
  messages: AiChatMessage[];
}

export interface ExplainReportResponse {
  explanation: string;
  citations: AiCitation[];
  disclaimer: string;
}
