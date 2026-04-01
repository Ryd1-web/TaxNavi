# TaxNavi.ai - Architecture Documentation

## System Overview

TaxNavi.ai is a comprehensive tax advisory platform built on a modern, scalable architecture combining Clean Architecture principles with cloud-native services.

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Frontend (Next.js 14)                         │
│  React Components │ Context API │ TailwindCSS │ API Service Layer   │
└─────────────────────────────────────────────────────────────────────┘
                              ↕ (HTTPS/REST)
┌─────────────────────────────────────────────────────────────────────┐
│                    Backend API (.NET 8 Web API)                      │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ Controllers (API Layer)                                     │    │
│  │ - AuthController                                            │    │
│  │ - TaxAssessmentController                                   │    │
│  │ - ReportsController                                         │    │
│  │ - PaymentsController                                        │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              ↕                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ Application Layer (Business Logic)                          │    │
│  │ - TaxCalculationService                                     │    │
│  │ - Validators (FluentValidation)                             │    │
│  │ - DTOs                                                      │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              ↕                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ Infrastructure Layer                                        │    │
│  │ - Repositories (EF Core)                                    │    │
│  │ - Azure OpenAI Service                                      │    │
│  │ - PDF Generation Service                                    │    │
│  │ - Blob Storage Service                                      │    │
│  │ - Authentication Service                                    │    │
│  │ - Payment Service (Paystack)                                │    │
│  └─────────────────────────────────────────────────────────────┘    │
│                              ↕                                       │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │ Domain Layer                                                │    │
│  │ - Domain Models (Entities)                                  │    │
│  │ - Domain Interfaces                                         │    │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
               ↓        ↓         ↓         ↓         ↓
            SQL     Azure      Blob      OpenAI    Paystack
           Server   OpenAI    Storage     API       API
```

## Clean Architecture Layers

### 1. Domain Layer (TaxNavi.Domain)
**Purpose**: Define business entities and contracts

**Responsibilities**:
- Define domain models: User, TaxProfile, TaxReport, Payment
- Define service interfaces (contracts)
- Define domain-specific exceptions
- No external dependencies (highly testable)

**Key Files**:
- `Entities/Entities.cs` - Domain models
- `Interfaces/IInterfaces.cs` - Service contracts

**Dependencies**: None (only .NET framework)

### 2. Application Layer (TaxNavi.Application)
**Purpose**: Implement business logic and use cases

**Responsibilities**:
- Tax calculations and logic
- Input validation (FluentValidation)
- Data Transfer Objects (DTOs)
- Application services

**Key Files**:
- `Services/TaxCalculationService.cs` - Core tax logic
- `DTOs/ApplicationDtos.cs` - Data contracts
- `Validators/Validators.cs` - Input validation

**Dependencies**: Domain layer only

### 3. Infrastructure Layer (TaxNavi.Infrastructure)
**Purpose**: Implement data access and external service integrations

**Responsibilities**:
- Entity Framework Core DbContext
- Repository pattern implementation
- Azure service integration
- PDF generation
- JWT token generation
- Payment service orchestration

**Key Files**:
- `Data/TaxNaviDbContext.cs` - Database configuration
- `Repositories/Repositories.cs` - Data access
- `Services/AzureServices.cs` - Cloud integrations
- `Services/PdfAndPaymentServices.cs` - Report and payment

**Dependencies**: Domain, Application, and external libraries

### 4. API Layer (TaxNavi.Api)
**Purpose**: Expose REST endpoints and configure the application

**Responsibilities**:
- HTTP controllers and endpoints
- Dependency injection setup
- Middleware configuration
- CORS and authentication setup
- Request/response handling

**Key Files**:
- `Controllers/ApiControllers.cs` - REST endpoints
- `Program.cs` - Application startup and DI

**Dependencies**: All layers below + ASP.NET Core

## Data Flow

### 1. Registration Flow
```
Frontend (Register Form)
    ↓
API Controller (POST /auth/register)
    ↓
Validator (FluentValidation)
    ↓
AuthenticationService
    ├→ Hash password (BCrypt)
    ├→ Create user entity
    ├→ Save to database (Repository)
    └→ Generate JWT token
    ↓
API Response (AuthResponse)
    ↓
Frontend (Store token, redirect to dashboard)
```

### 2. Tax Assessment Flow
```
Frontend (Assessment Form)
    ↓
API Controller (POST /taxassessment/individual)
    ↓
Validator
    ↓
TaxCalculationService
    ├→ Calculate gross income
    ├→ Calculate deductions
    ├→ Calculate consolidated relief
    ├→ Apply tax bands
    └→ Return calculation result
    ↓
AzureAiTaxAdvisorService
    ├→ Generate human-readable summary
    ├→ Generate savings advice
    └→ Store in database
    ↓
PdfReportService
    ├→ Generate PDF
    ├→ Upload to Blob Storage
    └→ Return download URL
    ↓
API Response (Calculation + Summary + Advice)
    ↓
Frontend (Display results with charts/tables)
```

### 3. Report Generation Flow
```
TaxCalculationService (calculations)
    ↓
AiTaxAdvisorService (AI insights)
    ↓
PdfReportService (PDF generation)
    ├→ Format data
    ├→ Generate PDF with QuestPDF
    ├→ Upload to Azure Blob
    └→ Get download URL
    ↓
TaxReport Entity (Save to database)
    ↓
Repository (Persist to SQL)
```

## Database Schema

### Users Table
```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(450) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    FirstName NVARCHAR(MAX) NOT NULL,
    LastName NVARCHAR(MAX) NOT NULL,
    PhoneNumber NVARCHAR(MAX),
    UserType NVARCHAR(MAX) DEFAULT 'Individual',
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    IsActive BIT DEFAULT 1
);
```

### TaxProfiles Table
```sql
CREATE TABLE TaxProfiles (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProfileType NVARCHAR(MAX) NOT NULL,
    JsonData NVARCHAR(MAX) DEFAULT '{}',
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
```

### TaxReports Table
```sql
CREATE TABLE TaxReports (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    TaxProfileId UNIQUEIDENTIFIER NOT NULL,
    PdfUrl NVARCHAR(MAX) NOT NULL,
    Summary NVARCHAR(MAX) NOT NULL,
    TaxLiability DECIMAL(18,2) NOT NULL,
    TotalIncome DECIMAL(18,2) NOT NULL,
    AllowableDeductions DECIMAL(18,2) NOT NULL,
    ProjectedSavings DECIMAL(18,2) NOT NULL,
    ReportType NVARCHAR(MAX) NOT NULL,
    JsonData NVARCHAR(MAX) DEFAULT '{}',
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (TaxProfileId) REFERENCES TaxProfiles(Id) ON DELETE CASCADE
);
```

### Payments Table
```sql
CREATE TABLE Payments (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    TaxReportId UNIQUEIDENTIFIER,
    Amount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(MAX) DEFAULT 'Pending',
    Reference NVARCHAR(MAX) NOT NULL,
    PaymentMethod NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
```

## API Architecture

### Authentication
- **Type**: JWT Bearer
- **Expiration**: 24 hours
- **Algorithm**: HS256
- **Token Structure**:
  - Subject: User ID
  - Email claim
  - User Type claim
  - Standard claims (iat, exp, iss, aud)

### Request/Response Format
```typescript
// Success Response
{
  success: boolean,
  data: T,
  message?: string
}

// Error Response
{
  success: false,
  message: string,
  errors?: string[]
}
```

## Service Integration

### Azure OpenAI Integration
```
TaxNavi Backend
    ↓
AzureAiTaxAdvisorService
    ├→ GenerateHumanReadableSummary
    ├→ GenerateSavingsAdvice
    └→ GenerateSMERoadmap
    ↓
Azure OpenAI API
    ↓
GPT-3.5-Turbo Model
    ↓
Response Text
```

### Azure Blob Storage Integration
```
PDF Generation
    ↓
Byte[] PDF Content
    ↓
BlobStorageService.UploadFileAsync
    ↓
Azure Blob Storage
    ↓
Download URL
```

### Paystack Payment Integration
```
PaymentService
    ├→ InitiatePayment (Create transaction)
    ├→ VerifyPayment (Check status)
    └→ UpdatePaymentStatus (Update database)
```

## Frontend Architecture

### Component Hierarchy
```
RootLayout
├── AuthProvider
│   ├── HomePage
│   ├── LoginPage
│   ├── RegisterPage
│   ├── DashboardPage
│   │   └── Navigation
│   ├── TaxAssessmentPages
│   │   ├── IndividualTaxPage
│   │   └── SMETaxPage
│   ├── ReportsPages
│   │   ├── ReportsListPage
│   │   └── ReportDetailPage
│   └── Navigation
```

### State Management
```
AuthContext (Global)
├── user: User | null
├── isAuthenticated: boolean
├── token: string | null
├── isLoading: boolean
└── Methods
    ├── login()
    ├── register()
    └── logout()

Component Local State (useState)
├── formData
├── results
├── isLoading
└── error
```

## Security Architecture

### Authentication & Authorization
```
User Login
    ↓
AuthenticationService.LoginAsync
    ├→ Verify password (BCrypt)
    ├→ Generate JWT token
    └→ Generate refresh token
    ↓
Frontend stores JWT in localStorage
    ↓
All API requests include Authorization header
    ↓
API validates JWT on protected routes
```

### Password Security
- BCrypt hashing (2^12 rounds)
- Never stored as plaintext
- Validated on login before comparison

### Data Protection
- JWT signatures prevent tampering
- HTTPS in production
- CORS restricts cross-origin requests
- Input validation on all endpoints

## Scalability Considerations

### Horizontal Scaling
1. **API Layer**: Stateless, can scale with load balancer
2. **Database**: Azure SQL with read replicas
3. **Storage**: Azure Blob, auto-scales
4. **AI Service**: Azure OpenAI, managed service

### Caching Strategy
1. JWT tokens cached client-side
2. Tax calculation results cached in-memory
3. Report PDFs cached in Blob Storage
4. Future: Redis for distributed caching

### Performance Optimization
1. Async/await for non-blocking operations
2. Pagination for large result sets
3. Lazy loading of reports
4. Response compression
5. Database query optimization with includes

## Deployment Architecture

### Docker Containers
```
docker-compose.yml
├── mssql
│   └── Port 1433
├── api
│   ├── Port 5000
│   └── Depends on: mssql
└── frontend
    ├── Port 3000
    └── Depends on: api
```

### Azure Deployment
```
Resource Group (taxnavi-rg)
├── App Service (API)
├── App Service (Frontend)
├── SQL Database
├── Blob Storage
└── OpenAI Resource
```

## Error Handling

### Backend Error Flow
```
Controller
    ↓
Try-Catch Block
    ├→ Validation errors (400)
    ├→ Authentication errors (401)
    ├→ Authorization errors (403)
    ├→ Not found errors (404)
    └→ Server errors (500)
    ↓
Log to console/application insights
    ↓
Return structured error response
```

### Frontend Error Handling
```
API Call
    ↓
Axios Interceptor
    ├→ Check response status
    └→ Redirect to login on 401
    ↓
Component Try-Catch
    ├→ Set error state
    └→ Display error message
    ↓
User sees error notification
```

## Monitoring & Logging

### Backend Logging
- Console logging (Development)
- Application Insights (Production)
- Logs include:
  - Request/response times
  - Database query times
  - API call latencies
  - Errors and exceptions

### Frontend Monitoring
- Browser console errors (Development)
- User actions tracking (Future)
- Performance metrics (Future)

## Testing Strategy

### Backend
```
Unit Tests
├── TaxCalculationService
├── Validators
└── Repositories

Integration Tests
├── Database operations
├── API endpoints
└── Azure service calls

E2E Tests (Future)
└── Complete user flows
```

### Frontend
```
Component Tests
├── Form components
├── Navigation
└── Report display

Integration Tests
├── Auth flow
├── API communication
└── State management

E2E Tests (Future)
└── Complete user workflows
```

## DevOps Pipeline

### CI/CD (Future)
```
Git Push
    ↓
GitHub Actions
    ├→ Run tests
    ├→ Build Docker images
    ├→ Push to registry
    └→ Deploy to Azure
    ↓
Automated Deployment
    ├→ API on App Service
    └→ Frontend on App Service
```

## Documentation Reference

- **API Docs**: See `API_SPECIFICATION.md`
- **Backend Setup**: See `backend/README.md`
- **Frontend Setup**: See `frontend/README.md`
- **Quick Start**: See `QUICKSTART.md`

---

This architecture ensures:
✅ Clean separation of concerns  
✅ Easy testing and maintenance  
✅ Scalability for growth  
✅ Security best practices  
✅ Cloud-native design  

Version: 1.0.0  
Last Updated: April 2024
