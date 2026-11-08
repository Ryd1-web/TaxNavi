export interface ChecklistItem {
  id: string;
  title: string;
  description: string;
  group: string;
}

export const individualDocuments: ChecklistItem[] = [
  { id: 'tin', group: 'Identity', title: 'Tax Identification Number (TIN)', description: 'Your tax ID. Individuals can use their NIN-linked tax ID; your employer or State IRS can confirm it.' },
  { id: 'nin', group: 'Identity', title: 'National Identification Number (NIN)', description: 'Used to verify your identity with tax authorities.' },
  { id: 'payslips', group: 'Income', title: 'Payslips for the year', description: 'Shows gross pay, PAYE deducted and statutory contributions.' },
  { id: 'paye-cert', group: 'Income', title: 'Employer PAYE / tax deduction card', description: 'Confirms the tax your employer deducted and remitted for you.' },
  { id: 'freelance-invoices', group: 'Income', title: 'Invoices and payment records', description: 'For freelance or business income — bank statements help reconcile.' },
  { id: 'pension-statement', group: 'Deductions', title: 'Pension statement (RSA)', description: 'From your Pension Fund Administrator, showing your contributions.' },
  { id: 'nhf-nhis', group: 'Deductions', title: 'NHF and health insurance records', description: 'Usually on your payslip; keep statements if paid separately.' },
  { id: 'tenancy', group: 'Deductions', title: 'Tenancy agreement and rent receipts', description: 'Needed to support a rent relief claim.' },
  { id: 'life-assurance', group: 'Deductions', title: 'Life assurance policy and premium receipts', description: 'For policies on your own or your spouse’s life.' },
  { id: 'mortgage', group: 'Deductions', title: 'Mortgage interest statement', description: 'From your lender, for a loan on the home you live in.' },
];

export const smeDocuments: ChecklistItem[] = [
  { id: 'cac', group: 'Registration', title: 'CAC registration certificate', description: 'Business name or company incorporation documents.' },
  { id: 'tin', group: 'Registration', title: 'Business Tax Identification Number (TIN)', description: 'Required to file returns and for VAT registration.' },
  { id: 'vat-reg', group: 'Registration', title: 'VAT registration (if required)', description: 'Needed once your turnover passes the small-business threshold.' },
  { id: 'bank', group: 'Records', title: 'Business bank statements', description: 'Keep business and personal accounts separate.' },
  { id: 'sales', group: 'Records', title: 'Sales invoices and receipts', description: 'Supports your turnover figure and VAT charged.' },
  { id: 'expenses', group: 'Records', title: 'Expense receipts', description: 'Every deductible cost needs evidence.' },
  { id: 'payroll', group: 'Records', title: 'Payroll records and PAYE remittance receipts', description: 'If you have employees.' },
  { id: 'assets', group: 'Records', title: 'Fixed asset register', description: 'Equipment, vehicles and property — used for capital allowances and small-company tests.' },
  { id: 'accounts', group: 'Annual', title: 'Annual financial statements', description: 'Companies file these with their company income tax return.' },
  { id: 'wht', group: 'Annual', title: 'Withholding tax credit notes', description: 'Tax deducted by your customers can offset your liability.' },
];
