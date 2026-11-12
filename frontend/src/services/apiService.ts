import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { progress } from '@/lib/progress';
import {
  AiChatResponse,
  AiConversation,
  AiConversationSummary,
  AssistantAnswer,
  ExplainReportResponse,
  CheckoutResult,
  PaymentsConfig,
  Entitlements,
  PaymentRecord,
  TaxCalculationRequest,
  TaxReportAccessResponse,
  TaxReportListItem,
  TaxRules,
  User,
} from '@/types';

export const TOKEN_KEY = 'taxnavi.token';
export const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

interface Envelope<T> {
  success: boolean;
  message?: string | null;
  code?: string | null;
  data: T;
}

export interface SessionData {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  userType: User['userType'];
  jwtToken: string;
  expiresIn: number;
}

export class ApiError extends Error {
  constructor(message: string, public status?: number, public code?: string | null, public data?: unknown) {
    super(message);
  }
}

/** Normalises any failure into a user-readable message. */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  const axiosError = error as AxiosError<Partial<Envelope<unknown>>>;
  if (axiosError?.isAxiosError) {
    if (!axiosError.response) {
      return new ApiError('We could not reach TaxNavi. Check your connection and try again.');
    }
    const { status, data } = axiosError.response;
    if (status === 429) return new ApiError('Too many attempts. Please wait a minute and try again.', status);
    return new ApiError(data?.message || 'Something went wrong. Please try again.', status, data?.code, data?.data);
  }
  return new ApiError('Something went wrong. Please try again.');
}

type TrackedConfig = InternalAxiosRequestConfig & { __progress?: boolean };

function finishTracked(config?: InternalAxiosRequestConfig) {
  const tracked = config as TrackedConfig | undefined;
  if (tracked?.__progress) {
    tracked.__progress = false;
    progress.requestFinished();
  }
}

class ApiService {
  private api: AxiosInstance;
  private onUnauthorized?: () => void;

  constructor() {
    this.api = axios.create({
      baseURL: API_BASE_URL,
      headers: { 'Content-Type': 'application/json' },
      timeout: 30_000,
    });

    this.api.interceptors.request.use((config) => {
      // Data loads (GETs) feed the top progress bar; mutations already show button spinners.
      if (typeof window !== 'undefined' && (config.method ?? 'get').toLowerCase() === 'get') {
        (config as TrackedConfig).__progress = true;
        progress.requestStarted();
      }
      if (typeof window !== 'undefined') {
        const token = window.localStorage.getItem(TOKEN_KEY);
        if (token) config.headers.Authorization = `Bearer ${token}`;
      }
      return config;
    });

    this.api.interceptors.response.use(
      (response) => {
        finishTracked(response.config);
        return response;
      },
      async (error: AxiosError) => {
        finishTracked(error.config);
        // Blob responses (PDF downloads) carry JSON error bodies as blobs.
        if (error.response?.data instanceof Blob) {
          try {
            error.response.data = JSON.parse(await error.response.data.text());
          } catch {
            error.response.data = {};
          }
        }
        if (error.response?.status === 401 && error.config?.headers?.Authorization) {
          this.onUnauthorized?.();
        }
        return Promise.reject(toApiError(error));
      },
    );
  }

  setUnauthorizedHandler(handler: () => void) {
    this.onUnauthorized = handler;
  }

  private async unwrap<T>(request: Promise<{ data: Envelope<T> }>): Promise<T> {
    const response = await request;
    return response.data.data;
  }

  // Auth & account
  register(body: { email: string; password: string; firstName: string; lastName: string; userType: string }) {
    return this.unwrap<SessionData>(this.api.post('/auth/register', body));
  }

  login(email: string, password: string) {
    return this.unwrap<SessionData>(this.api.post('/auth/login', { email, password }));
  }

  me() {
    return this.unwrap<User & { createdAt: string }>(this.api.get('/account/me'));
  }

  updateProfile(body: { firstName: string; lastName: string; userType: string }) {
    return this.unwrap<User>(this.api.put('/account/me', body));
  }

  entitlements() {
    return this.unwrap<Entitlements>(this.api.get('/account/entitlements'));
  }

  // Tax
  rules(year?: number) {
    return this.unwrap<TaxRules>(this.api.get('/tax/rules', { params: year ? { year } : undefined }));
  }

  calculateFree(request: TaxCalculationRequest) {
    return this.unwrap<TaxReportAccessResponse>(this.api.post('/tax/calculate/free', request));
  }

  saveReport(request: TaxCalculationRequest) {
    return this.unwrap<TaxReportAccessResponse>(this.api.post('/tax/reports', request));
  }

  ask(question: string, options: { taxYear?: number; calculation?: TaxCalculationRequest } = {}) {
    return this.unwrap<AssistantAnswer>(this.api.post('/tax/assistant', { question, ...options }));
  }

  // AI assistant (RAG)
  aiChat(body: {
    question: string;
    conversationId?: string;
    guestToken?: string;
    taxYear?: number;
    calculation?: TaxCalculationRequest;
  }) {
    return this.unwrap<AiChatResponse>(this.api.post('/ai/assistant/chat', body));
  }

  aiConversations() {
    return this.unwrap<AiConversationSummary[]>(this.api.get('/ai/assistant/conversations'));
  }

  aiConversation(id: string, guestToken?: string) {
    return this.unwrap<AiConversation>(
      this.api.get(`/ai/assistant/conversations/${id}`, {
        headers: guestToken ? { 'X-Conversation-Token': guestToken } : undefined,
      }),
    );
  }

  explainReport(calculation: TaxCalculationRequest) {
    return this.unwrap<ExplainReportResponse>(this.api.post('/ai/assistant/explain-report', { calculation }));
  }

  // Reports
  reports() {
    return this.unwrap<TaxReportListItem[]>(this.api.get('/reports'));
  }

  report(id: string) {
    return this.unwrap<{ createdAt: string; report: TaxReportAccessResponse }>(this.api.get(`/reports/${id}`));
  }

  deleteReport(id: string) {
    return this.unwrap<null>(this.api.delete(`/reports/${id}`));
  }

  async downloadReportPdf(id: string) {
    const response = await this.api.get(`/reports/${id}/pdf`, { responseType: 'blob' });
    const disposition = String(response.headers['content-disposition'] || '');
    const name = /filename="?([^";]+)"?/i.exec(disposition)?.[1] || `taxnavi-report-${id}.pdf`;
    return { blob: response.data as Blob, fileName: name };
  }

  // Payments (amounts are always decided by the server)
  paymentsConfig() {
    return this.unwrap<PaymentsConfig>(this.api.get('/payments/config'));
  }

  checkout(body: {
    purpose: 'PremiumReport' | 'SmeSubscription';
    taxReportId?: string;
    accessToken?: string;
    email?: string;
    calculation?: TaxCalculationRequest;
  }) {
    return this.unwrap<CheckoutResult>(this.api.post('/payments/checkout', body));
  }

  /** Resolves with the HTTP status so callers can show pending / cancelled / failed states. */
  verifyPayment(reference: string) {
    return this.api.post<Envelope<PaymentRecord>>('/payments/verify', { reference }, { validateStatus: (s) => s < 500 });
  }

  paymentHistory() {
    return this.unwrap<PaymentRecord[]>(this.api.get('/payments/history'));
  }

  // Guest reports (bought without an account) — the access token proves ownership.
  guestReport(id: string, accessToken: string) {
    return this.unwrap<{ createdAt: string; isGuest: boolean; report: TaxReportAccessResponse }>(
      this.api.get(`/reports/guest/${id}`, { headers: { 'X-Report-Access-Token': accessToken } }),
    );
  }

  async downloadGuestReportPdf(id: string, accessToken: string) {
    const response = await this.api.get(`/reports/guest/${id}/pdf`, { responseType: 'blob', headers: { 'X-Report-Access-Token': accessToken } });
    const disposition = String(response.headers['content-disposition'] || '');
    const name = /filename="?([^";]+)"?/i.exec(disposition)?.[1] || `taxnavi-report-${id}.pdf`;
    return { blob: response.data as Blob, fileName: name };
  }

  claimReport(id: string, accessToken: string) {
    return this.unwrap<string>(this.api.post(`/reports/${id}/claim`, null, { headers: { 'X-Report-Access-Token': accessToken } }));
  }

  // SME subscription
  subscriptionCheckout() {
    return this.unwrap<CheckoutResult>(this.api.post('/subscriptions/checkout'));
  }

  cancelSubscription() {
    return this.api.post<Envelope<boolean>>('/subscriptions/cancel').then((r) => r.data);
  }

  subscriptionManageLink() {
    return this.unwrap<string>(this.api.get('/subscriptions/manage-link'));
  }
}

export const apiService = new ApiService();
