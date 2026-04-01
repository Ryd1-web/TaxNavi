# Frontend Development Guide

## Project Structure

```
src/
├── app/                          # Next.js pages (App Router)
│   ├── layout.tsx               # Root layout with AuthProvider
│   ├── page.tsx                 # Home page
│   ├── auth/
│   │   ├── login/page.tsx
│   │   └── register/page.tsx
│   ├── dashboard/
│   │   └── page.tsx
│   ├── tax/
│   │   ├── individual/page.tsx
│   │   └── sme/page.tsx
│   └── reports/
│       ├── page.tsx
│       └── [id]/page.tsx
├── components/                   # Reusable React components
│   └── Navigation.tsx
├── context/                      # React Context API
│   └── AuthContext.tsx
├── services/                     # API calls
│   └── apiService.ts
├── types/                        # TypeScript interfaces
│   └── index.ts
└── styles/                       # Global styles
    └── globals.css
```

## Key Features

### 1. Authentication Flow

**Provider**: `src/context/AuthContext.tsx`

```typescript
export function useAuth() {
  const { user, isAuthenticated, login, register, logout } = useAuth();
}
```

Features:
- JWT token storage in localStorage
- Auto-login on page refresh
- Token in Authorization header for API calls
- Refresh token support

### 2. API Integration

**Service**: `src/services/apiService.ts`

```typescript
// Example usage
const response = await apiService.assessIndividualTax({
  monthlySalary: 500000,
  // ... other fields
});
```

Auto-includes JWT token in all requests via Axios interceptor.

### 3. Form Handling

Using React Hook Form for optimal performance:

```typescript
const { register, handleSubmit, watch } = useForm<FormData>();

const onSubmit = async (data: FormData) => {
  // Submit form
};

return (
  <form onSubmit={handleSubmit(onSubmit)}>
    <input {...register('fieldName')} />
  </form>
);
```

### 4. Styling

TailwindCSS utility-first approach with custom components in `src/styles/globals.css`:

```html
<button className="btn btn-primary">
  Click me
</button>
```

Available utility classes:
- `.btn`, `.btn-primary`, `.btn-secondary`, `.btn-danger`, `.btn-success`
- `.form-input`, `.form-label`
- `.card`
- `.alert`, `.alert-success`, `.alert-error`, `.alert-info`

## Pages Overview

### Authentication Pages

#### Login (`/auth/login`)
- Email and password input
- Form validation
- Error display
- Link to register

#### Register (`/auth/register`)
- First name, last name, email, password
- Account type selection (Individual/SME)
- Password requirements display
- Link to login

### Dashboard (`/dashboard`)
- Welcome message
- Quick action cards
- Recent tax reports table
- Summary statistics

### Tax Assessment Pages

#### Individual Tax (`/tax/individual`)
- Income input form
- Expense input form
- Real-time calculation
- Results display with:
  - Total income
  - Tax liability
  - Deductions
  - Projected savings
  - AI-generated advice

#### SME Tax (`/tax/sme`)
- Business information form
- Financial data inputs
- VAT eligibility display
- Tax obligations breakdown
- Compliance roadmap

### Reports Pages

#### Reports List (`/reports`)
- All user reports
- Tax liability summary
- Download links
- Click to view details

#### Report Detail (`/reports/[id]`)
- Detailed assessment breakdown
- Summary information
- PDF download link
- Print functionality

## Components

### Navigation Component

```typescript
<Navigation user={user} onLogout={logout} />
```

Displays:
- App logo and links
- User name
- Logout button

## State Management

### Context API (AuthContext)

```typescript
const AuthContext = {
  user: User | null,
  isAuthenticated: boolean,
  isLoading: boolean,
  login: (email, password) => Promise<void>,
  register: (...) => Promise<void>,
  logout: () => void,
  token: string | null,
}
```

### Component-Level State

Use React hooks for local component state:

```typescript
const [formData, setFormData] = useState<FormData>({...});
const [isLoading, setIsLoading] = useState(false);
const [error, setError] = useState('');
```

## API Service

### Base Configuration

```typescript
const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL,
  headers: { 'Content-Type': 'application/json' },
});
```

### Request Interceptor

Auto-adds JWT token:
```typescript
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('jwtToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

### Usage Example

```typescript
const response = await apiService.login('user@example.com', 'password');
const { data } = response; // AuthResponse
```

## Environment Variables

Create `.env.local`:

```
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

Note: Only variables prefixed with `NEXT_PUBLIC_` are available in browser.

## Development Workflow

1. **Start dev server**
```bash
npm run dev
```

2. **Make changes** to components/pages

3. **Hot reload** automatically applies changes

4. **Build for production**
```bash
npm run build
npm start
```

## TypeScript Types

All types defined in `src/types/index.ts`:

```typescript
interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  userType: 'Individual' | 'SME';
}

interface TaxReport {
  id: string;
  taxLiability: number;
  totalIncome: number;
  // ...
}
```

## Error Handling

### Try-Catch Pattern

```typescript
try {
  const response = await apiService.assessIndividualTax(data);
  setResult(response.data.data);
} catch (err: any) {
  setError(err.response?.data?.message || 'An error occurred');
}
```

### Validation with Async Validators

```typescript
const validationResult = await validator.ValidateAsync(data);
if (!validationResult.IsValid) {
  setErrors(validationResult.errors);
}
```

## Performance Optimization

### 1. Code Splitting

Next.js automatically splits code at page boundaries.

### 2. Image Optimization

```typescript
import Image from 'next/image';

<Image src="/logo.png" alt="Logo" width={100} height={100} />
```

### 3. API Caching

```typescript
// Cache API responses
const response = await apiService.getReports();
sessionStorage.setItem('reports', JSON.stringify(response.data));
```

### 4. Lazy Loading

```typescript
import dynamic from 'next/dynamic';

const Component = dynamic(() => import('./Component'));
```

## Testing

### Component Test Example

```typescript
import { render, screen } from '@testing-library/react';
import LoginPage from './page';

test('renders login form', () => {
  render(<LoginPage />);
  expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
});
```

### Run Tests

```bash
npm test
npm test -- --coverage
```

## Deployment

### Vercel Deployment

```bash
# Connect to Vercel
vercel link

# Deploy
vercel deploy --prod
```

### Docker Deployment

```bash
docker build -t taxnavi-frontend .
docker run -p 3000:3000 taxnavi-frontend
```

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Common Issues

### Issue: "Cannot find module"
```
Solution: Check import paths and file extensions in tsconfig.json
```

### Issue: API 401 Unauthorized
```
Solution: JWT token expired or invalid, user needs to login again
```

### Issue: CORS error
```
Solution: Backend needs CORS configuration for frontend domain
```

### Issue: Blank page on production
```
Solution: Check NEXT_PUBLIC_API_URL in .env.production
```

## Best Practices

1. **Use TypeScript**: Leverage type safety
2. **Component Composition**: Break into smaller reusable components
3. **Error Handling**: Always include try-catch and error states
4. **Loading States**: Show loading indicators during async operations
5. **Validation**: Validate inputs before sending to API
6. **Accessibility**: Use semantic HTML and ARIA labels
7. **Performance**: Minimize rerenders with useMemo and useCallback
8. **Security**: Never store sensitive data in localStorage
9. **Responsive Design**: Test on mobile, tablet, and desktop
10. **Testing**: Write tests for critical user flows

## Git Workflow

```bash
# Create feature branch
git checkout -b feature/feature-name

# Make changes and commit
git add .
git commit -m "feat: description"

# Push and create PR
git push origin feature/feature-name
```

## Documentation

- [Next.js Docs](https://nextjs.org/docs)
- [React Docs](https://react.dev)
- [TailwindCSS Docs](https://tailwindcss.com/docs)
- [TypeScript Docs](https://www.typescriptlang.org/docs)

---

For more information, see the main [README.md](../README.md)
