import axios, { AxiosInstance } from 'axios';

class ApiService {
  private api: AxiosInstance;

  constructor() {
    this.api = axios.create({
      baseURL: process.env.NEXT_PUBLIC_API_URL,
      headers: {
        'Content-Type': 'application/json',
      },
      withCredentials: true,
    });

    // Add request interceptor to include JWT token
    this.api.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem('jwtToken');
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => {
        return Promise.reject(error);
      }
    );
  }

  // Auth endpoints
  async register(email: string, password: string, firstName: string, lastName: string, userType: string) {
    return this.api.post('/auth/register', {
      email,
      password,
      firstName,
      lastName,
      userType,
    });
  }

  async login(email: string, password: string) {
    return this.api.post('/auth/login', {
      email,
      password,
    });
  }

  // Tax assessment endpoints
  async assessIndividualTax(data: any) {
    return this.api.post('/taxassessment/individual', data);
  }

  async assessSMETax(data: any) {
    return this.api.post('/taxassessment/sme', data);
  }

  // Reports endpoints
  async getReports() {
    return this.api.get('/reports/user');
  }

  async getReport(id: string) {
    return this.api.get(`/reports/${id}`);
  }

  async generatePdfReport(data: any) {
    return this.api.post('/reports/generate-pdf', data);
  }

  // Payment endpoints
  async initiatePayment(data: any) {
    return this.api.post('/payments/initiate', data);
  }

  async verifyPayment(reference: string) {
    return this.api.post('/payments/verify', { reference });
  }
}

export const apiService = new ApiService();
