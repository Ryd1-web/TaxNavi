# TaxNavi API Specification

## Base URL
- Development: `http://localhost:5000/api`
- Production: `https://api.taxnavi.ai/api`

## Authentication
All endpoints except `/auth/register` and `/auth/login` require JWT Bearer token in Authorization header:

```
Authorization: Bearer {jwtToken}
```

## Response Format

### Success Response
```json
{
  "success": true,
  "data": { /* endpoint-specific data */ }
}
```

### Error Response
```json
{
  "success": false,
  "message": "Error description"
}
```

---

## Endpoints

### Authentication Endpoints

#### Register
- **POST** `/auth/register`
- **Description**: Create a new user account
- **Auth**: None

**Request Body**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123",
  "firstName": "John",
  "lastName": "Doe",
  "userType": "Individual|SME"
}
```

**Password Requirements**
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit

**Response**
```json
{
  "success": true,
  "message": "Registration successful",
  "jwtToken": "eyJhbGc...",
  "refreshToken": "base64EncodedToken",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "userType": "Individual"
  }
}
```

---

#### Login
- **POST** `/auth/login`
- **Description**: Authenticate user and receive JWT token
- **Auth**: None

**Request Body**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123"
}
```

**Response**
```json
{
  "success": true,
  "message": "Login successful",
  "jwtToken": "eyJhbGc...",
  "refreshToken": "base64EncodedToken",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "userType": "Individual"
  }
}
```

---

#### Refresh Token
- **POST** `/auth/refresh-token`
- **Description**: Get new JWT token using refresh token
- **Auth**: Required

**Request Body**
```json
{
  "refreshToken": "base64EncodedToken"
}
```

**Response**
```json
{
  "jwtToken": "newJwtToken",
  "refreshToken": "newRefreshToken"
}
```

---

### Tax Assessment Endpoints

#### Individual Tax Assessment
- **POST** `/taxassessment/individual`
- **Description**: Calculate personal tax liability
- **Auth**: Required

**Request Body**
```json
{
  "monthlySalary": 500000,
  "annualBonus": 1000000,
  "allowances": 200000,
  "businessIncome": 2000000,
  "freelanceIncome": 500000,
  "investmentIncome": 100000,
  "rentPaid": 600000,
  "loanInterest": 50000,
  "otherExpenses": 200000
}
```

**Response**
```json
{
  "success": true,
  "data": {
    "taxLiability": 2750000,
    "totalIncome": 5500000,
    "allowableDeductions": 850000,
    "consolidatedRelief": 200000,
    "projectedSavings": 137500,
    "taxBands": {
      "Band 1 (7%)": 21000,
      "Band 2 (11%)": 33000,
      "Band 3 (15%)": 60000,
      "Band 4 (19%)": 380000,
      "Band 5 (21%)": 630000,
      "Band 6 (24%)": 1626000
    },
    "aiSummary": "Based on your assessment...",
    "savingsAdvice": "You can optimize your tax by..."
  }
}
```

---

#### SME Tax Assessment
- **POST** `/taxassessment/sme`
- **Description**: Calculate SME tax obligations and compliance roadmap
- **Auth**: Required

**Request Body**
```json
{
  "businessName": "ABC Trading Ltd",
  "businessType": "Retail|Service|Manufacturing|Technology|Other",
  "annualTurnover": 50000000,
  "operatingExpenses": 10000000,
  "payrollCost": 5000000,
  "rentAndUtilities": 2000000,
  "assets": 20000000,
  "vatableIncome": 40000000
}
```

**Response**
```json
{
  "success": true,
  "data": {
    "vatEligible": true,
    "vatLiability": 3000000,
    "whtLiability": 500000,
    "totalTaxObligation": 12000000,
    "complianceRoadmap": "Month-by-month compliance..."
  }
}
```

---

### Reports Endpoints

#### Get All Reports
- **GET** `/reports`
- **Description**: Retrieve all tax reports for authenticated user
- **Auth**: Required
- **Query Parameters**:
  - `skip` (optional): Number of records to skip (default: 0)
  - `take` (optional): Number of records to return (default: 10)

**Response**
```json
{
  "success": true,
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "userId": "user-id",
      "reportType": "Individual",
      "taxLiability": 2750000,
      "totalIncome": 5500000,
      "allowableDeductions": 850000,
      "projectedSavings": 137500,
      "pdfUrl": "https://...",
      "createdAt": "2024-04-01T10:00:00Z"
    }
  ]
}
```

---

#### Get Report by ID
- **GET** `/reports/{id}`
- **Description**: Retrieve specific tax report
- **Auth**: Required
- **Path Parameters**:
  - `id`: Report UUID

**Response**
```json
{
  "success": true,
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "userId": "user-id",
    "reportType": "Individual",
    "taxLiability": 2750000,
    "totalIncome": 5500000,
    "allowableDeductions": 850000,
    "projectedSavings": 137500,
    "pdfUrl": "https://...",
    "summary": "Tax assessment summary text...",
    "createdAt": "2024-04-01T10:00:00Z"
  }
}
```

---

### Payment Endpoints

#### Initiate Payment
- **POST** `/payments/initiate`
- **Description**: Initiate payment transaction (Paystack)
- **Auth**: Required

**Request Body**
```json
{
  "amount": 50000,
  "taxReportId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response**
```json
{
  "success": true,
  "data": {
    "reference": "PAY-1234567890",
    "authorizationUrl": "https://checkout.paystack.com/...",
    "accessCode": "ABCD1234"
  }
}
```

---

#### Verify Payment
- **POST** `/payments/verify`
- **Description**: Verify payment transaction status
- **Auth**: Required

**Request Body**
```json
{
  "reference": "PAY-1234567890"
}
```

**Response**
```json
{
  "success": true,
  "data": {
    "success": true,
    "status": "success|failed",
    "reference": "PAY-1234567890"
  }
}
```

---

## Error Codes

| Status | Code | Message |
|--------|------|---------|
| 400 | BAD_REQUEST | Invalid request parameters |
| 401 | UNAUTHORIZED | Missing or invalid JWT token |
| 403 | FORBIDDEN | User doesn't have access to resource |
| 404 | NOT_FOUND | Resource not found |
| 409 | CONFLICT | Email already registered |
| 422 | VALIDATION_FAILED | Input validation failed |
| 500 | INTERNAL_ERROR | Server error |

---

## Rate Limiting

- **Limit**: 100 requests per minute
- **Headers**: 
  - `X-RateLimit-Limit`: Request limit
  - `X-RateLimit-Remaining`: Remaining requests
  - `X-RateLimit-Reset`: Time until reset (Unix timestamp)

---

## Pagination

Endpoints that return lists support pagination:

**Query Parameters**
- `skip`: Number of records to skip (default: 0)
- `take`: Number of records per page (default: 20, max: 100)

**Response Metadata**
```json
{
  "success": true,
  "data": [...],
  "pagination": {
    "total": 50,
    "skip": 0,
    "take": 20,
    "hasMore": true
  }
}
```

---

## Sorting

**Query Parameter**: `sortBy` and `sortOrder`

Example: `/reports?sortBy=createdAt&sortOrder=desc`

---

## Date Format

All timestamps use ISO 8601 format: `2024-04-01T10:00:00Z`

---

## Nigerian Tax Calculations

### Personal Income Tax (PIT)
- **Consolidated Relief**: ₦200,000 or 1% of gross income (whichever is higher)
  - Maximum: 20% of gross income
- **Tax Bands** (on taxable income):
  - First ₦300,000: 7%
  - Next ₦300,000: 11%
  - Next ₦400,000: 15%
  - Next ₦2,000,000: 19%
  - Next ₦3,000,000: 21%
  - Over ₦6,000,000: 24%

### Value Added Tax (VAT)
- **Registration Threshold**: Annual turnover > ₦25,000,000
- **Standard Rate**: 7.5%
- **Exempt Supplies**: Certain services exempt from VAT

### Withholding Tax (WHT)
- **Rate**: 5-10% depending on transaction category
- **Applicability**: On certain income and expense categories

---

## Usage Examples

### cURL

```bash
# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass123"
  }'

# Assess Individual Tax (with JWT token)
curl -X POST http://localhost:5000/api/taxassessment/individual \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "monthlySalary": 500000,
    "annualBonus": 1000000
  }'
```

### JavaScript/Axios

```javascript
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5000/api'
});

// Login
const loginResponse = await api.post('/auth/login', {
  email: 'user@example.com',
  password: 'SecurePass123'
});

const token = loginResponse.data.jwtToken;

// Add token to headers
api.defaults.headers.common['Authorization'] = `Bearer ${token}`;

// Assess tax
const taxResponse = await api.post('/taxassessment/individual', {
  monthlySalary: 500000,
  annualBonus: 1000000
});
```

### Python/Requests

```python
import requests

BASE_URL = 'http://localhost:5000/api'

# Login
response = requests.post(f'{BASE_URL}/auth/login', json={
    'email': 'user@example.com',
    'password': 'SecurePass123'
})
token = response.json()['jwtToken']

# Assess tax
headers = {'Authorization': f'Bearer {token}'}
response = requests.post(f'{BASE_URL}/taxassessment/individual', 
    json={'monthlySalary': 500000},
    headers=headers
)
```

---

## Changelog

### Version 1.0.0 (2024-04-01)
- Initial release with individual and SME tax assessment
- JWT authentication
- PDF report generation
- Azure AI integration
- Paystack payment integration

---

For more documentation, visit [Full API Documentation](./API_DOCUMENTATION.md)
