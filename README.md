# TaxNavi.ai - AI-Powered Tax Advisory Platform

A comprehensive web application for AI-powered tax advisory tailored for individuals and SMEs in Nigeria. This project combines modern technology with Nigerian tax expertise to provide accurate, intelligent tax calculations and compliance guidance.

## 🎯 Features

### For Individuals
- **Income Assessment**: Calculate PIT/PAYE liability based on employment, business, and investment income
- **Deduction Optimization**: Identify allowable deductions to minimize tax burden
- **Consolidated Relief**: Automatic calculation of consolidated relief (₦200k or 1% of gross income)
- **Tax Bands**: Accurate application of Nigerian tax bands (7%, 11%, 15%, 19%, 21%, 24%)
- **AI Insights**: Personalized tax optimization advice
- **PDF Reports**: Download comprehensive tax assessment reports

### For SMEs
- **VAT Eligibility**: Determine VAT registration requirements (turnover > ₦25M)
- **Tax Obligations**: Calculate VAT, WHT, and CIT liabilities
- **Compliance Roadmap**: Month-by-month compliance guidance
- **Record Management**: Expense categorization and tracking

### Cross-Platform Features
- **Authentication**: Secure JWT-based authentication
- **Azure AI Integration**: Personalized insights using Azure OpenAI
- **PDF Generation**: Professional tax reports with QuestPDF
- **Payment Integration**: Paystack integration (placeholder for extension)
- **Blob Storage**: PDF reports stored in Azure Blob Storage

## 🛠️ Tech Stack

### Backend
- **Runtime**: .NET 8
- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)
- **Database**: PostgreSQL 16 (Render Postgres in production, Docker locally)
- **ORM**: Entity Framework Core 8.0
- **Authentication**: JWT with BCrypt password hashing
- **AI**: Azure OpenAI SDK
- **Storage**: Azure Blob Storage SDK
- **PDF**: QuestPDF
- **Validation**: FluentValidation

### Frontend
- **Framework**: Next.js 14 with TypeScript
- **Styling**: TailwindCSS
- **HTTP Client**: Axios
- **Form Management**: React Hook Form
- **State Management**: React Context API

### Deployment
- **Container**: Docker & Docker Compose
- **Database**: PostgreSQL 16

## 📋 Prerequisites

### Development
- **.NET 8 SDK** [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 20+** [Download](https://nodejs.org/)
- **PostgreSQL 16** (or `docker compose up -d db`)
- **Docker** (optional, for containerized deployment)
- **Azure Account** (optional, for cloud services)

### Azure Services (Optional)
- Azure SQL Database
- Azure Blob Storage
- Azure OpenAI

## 🚀 Getting Started

### Backend Setup

1. **Navigate to backend directory**
```bash
cd backend
```

2. **Restore dependencies**
```bash
dotnet restore
```

3. **Configure database connection**
Edit `src/Api/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=taxnavi;Username=taxnavi;Password=taxnavi_dev"
  }
}
```

4. **Apply migrations**
```bash
cd src/Infrastructure
dotnet ef database update --startup-project ../Api/TaxNavi.Api.csproj
```

5. **Configure environment variables** (optional for Azure services)
```bash
# Windows
set JWT_SECRET=your-secret-key
set AZURE_OPENAI_API_KEY=your-azure-key
set AZURE_OPENAI_ENDPOINT=your-azure-endpoint
set AZURE_BLOB_STORAGE_CONNECTION_STRING=your-connection-string

# Linux/Mac
export JWT_SECRET=your-secret-key
export AZURE_OPENAI_API_KEY=your-azure-key
export AZURE_OPENAI_ENDPOINT=your-azure-endpoint
export AZURE_BLOB_STORAGE_CONNECTION_STRING=your-connection-string
```

6. **Build and run**
```bash
cd src/Api
dotnet build
dotnet run
```

API will be available at: `http://localhost:5000`
Swagger UI: `http://localhost:5000/swagger`

### Frontend Setup

1. **Navigate to frontend directory**
```bash
cd frontend
```

2. **Install dependencies**
```bash
npm install
```

3. **Configure API endpoint** (optional)
Edit `.env.local`:
```
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

4. **Run development server**
```bash
npm run dev
```

Frontend will be available at: `http://localhost:3000`

## 🐳 Docker Deployment

### Using Docker Compose

1. **Set up environment variables**
```bash
cp .env.example .env
# Edit .env with your Azure credentials
```

2. **Build and run containers**
```bash
docker-compose up --build
```

3. **Access services**
- Frontend: http://localhost:3000
- API: http://localhost:5000
- Database: localhost:1433

### Manual Docker Build

**Backend**
```bash
cd backend
docker build -t taxnavi-api .
docker run -p 5000:5000 -e ConnectionStrings__DefaultConnection="..." taxnavi-api
```

**Frontend**
```bash
cd frontend
docker build -t taxnavi-frontend .
docker run -p 3000:3000 taxnavi-frontend
```

## 📚 API Documentation

### Authentication Endpoints

**Register**
```
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123",
  "firstName": "John",
  "lastName": "Doe",
  "userType": "Individual"
}
```

**Login**
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123"
}
```

### Tax Assessment Endpoints

**Individual Tax Assessment**
```
POST /api/taxassessment/individual
Authorization: Bearer {token}

{
  "monthlySalary": 500000,
  "annualBonus": 1000000,
  "allowances": 200000,
  "businessIncome": 2000000,
  "freelanceIncome": 500000,
  "investmentIncome": 100000,
  "rentPaid": 600000,
  "loanInterest": 0,
  "otherExpenses": 200000
}
```

**SME Tax Assessment**
```
POST /api/taxassessment/sme
Authorization: Bearer {token}

{
  "businessName": "ABC Trading Ltd",
  "businessType": "Retail",
  "annualTurnover": 50000000,
  "operatingExpenses": 10000000,
  "payrollCost": 5000000,
  "rentAndUtilities": 2000000,
  "assets": 20000000,
  "vatableIncome": 40000000
}
```

### Report Endpoints

**Get All Reports**
```
GET /api/reports
Authorization: Bearer {token}
```

**Get Report by ID**
```
GET /api/reports/{id}
Authorization: Bearer {token}
```

### Payment Endpoints

**Initiate Payment**
```
POST /api/payments/initiate
Authorization: Bearer {token}

{
  "amount": 50000,
  "taxReportId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Verify Payment**
```
POST /api/payments/verify
Authorization: Bearer {token}

{
  "reference": "PAY-1234567890"
}
```

## 📁 Project Structure

```
TaxNavi/
├── backend/
│   ├── src/
│   │   ├── Domain/              # Domain models and interfaces
│   │   │   ├── Entities/
│   │   │   └── Interfaces/
│   │   ├── Application/         # Business logic and DTOs
│   │   │   ├── Services/
│   │   │   ├── DTOs/
│   │   │   └── Validators/
│   │   ├── Infrastructure/      # Data access and external services
│   │   │   ├── Data/
│   │   │   ├── Repositories/
│   │   │   ├── Services/
│   │   │   └── Migrations/
│   │   └── Api/                 # Controllers and configuration
│   │       └── Controllers/
│   ├── TaxNavi.sln
│   └── Dockerfile
├── frontend/
│   ├── src/
│   │   ├── app/                 # Next.js pages
│   │   │   ├── auth/
│   │   │   ├── dashboard/
│   │   │   ├── tax/
│   │   │   └── reports/
│   │   ├── components/          # React components
│   │   ├── context/             # Context API
│   │   ├── services/            # API service layer
│   │   ├── types/               # TypeScript types
│   │   └── styles/              # Global styles
│   ├── package.json
│   ├── Dockerfile
│   └── next.config.js
├── docker-compose.yml
└── README.md
```

## 🔐 Security Considerations

1. **JWT Secret**: Change the default JWT secret in production
   - Generate: `openssl rand -base64 32`

2. **Password Policy**: Minimum 8 characters with uppercase, lowercase, and digits

3. **CORS**: Configure allowed origins for production

4. **HTTPS**: Use HTTPS in production (Azure App Service, etc.)

5. **Environment Variables**: Never commit sensitive data; use .env files

6. **Rate Limiting**: Implement rate limiting for production

7. **Input Validation**: All inputs are validated using FluentValidation

## 🧪 Testing

### Backend Testing
```bash
# Run tests
dotnet test

# Run specific test class
dotnet test --filter TestClassName
```

### Frontend Testing
```bash
# Run tests
npm test

# Run tests with coverage
npm test -- --coverage
```

## 📊 Nigerian Tax Regulations Implementation

### Personal Income Tax (PIT)
- **Consolidated Relief**: ₦200,000 or 1% of gross income (whichever is higher), capped at 20%
- **Tax Bands**:
  - First ₦300,000: 7%
  - Next ₦300,000: 11%
  - Next ₦400,000: 15%
  - Next ₦2,000,000: 19%
  - Next ₦3,000,000: 21%
  - Balance: 24%

### Value Added Tax (VAT)
- **Registration Threshold**: Annual turnover > ₦25,000,000
- **Tax Rate**: 7.5% on vatable supplies

### Withholding Tax (WHT)
- **Rates**: 5-10% depending on transaction type

### Corporate Income Tax (CIT)
- **Standard Rate**: 30% on taxable profit

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For issues and questions:
- Open an issue on GitHub
- Email: support@taxnavi.ai

## 🚀 Deployment to Azure

### App Service Deployment

1. **Create resource group**
```bash
az group create --name taxnavi-rg --location eastus
```

2. **Create App Service Plan**
```bash
az appservice plan create --name taxnavi-plan --resource-group taxnavi-rg --sku B2 --is-linux
```

3. **Create Web Apps**
```bash
# Backend
az webapp create --resource-group taxnavi-rg --plan taxnavi-plan --name taxnavi-api --runtime "DOTNETCORE|8.0"

# Frontend
az webapp create --resource-group taxnavi-rg --plan taxnavi-plan --name taxnavi-ui --runtime "NODE|20-lts"
```

4. **Configure database**
```bash
az sql server create --resource-group taxnavi-rg --name taxnavi-sqlserver --admin-user admin --admin-password YourPassword123
az sql db create --resource-group taxnavi-rg --server taxnavi-sqlserver --name TaxNavi
```

5. **Deploy from Git**
```bash
az webapp deployment source config-zip --resource-group taxnavi-rg --name taxnavi-api --src backend.zip
az webapp deployment source config-zip --resource-group taxnavi-rg --name taxnavi-ui --src frontend.zip
```

## 📅 Roadmap

- [ ] Mobile app (React Native)
- [ ] Advanced tax planning scenarios
- [ ] Real Paystack integration
- [ ] Receipt OCR using Azure Document Intelligence
- [ ] Multi-user business accounts
- [ ] Audit trail and compliance reports
- [ ] Real-time tax updates
- [ ] Integration with Nigerian tax authority systems

---

**Version**: 1.0.0  
**Last Updated**: April 2024  
**Author**: TaxNavi Development Team

## Configuration notes (2026 update)

- **Database:** apply the new migration before running against an existing database:
  `dotnet ef database update --project backend/src/Infrastructure --startup-project backend/src/Api`
  For a throwaway local run, use `UseInMemoryDatabase=true` and `DatabaseSetup__ApplyOnStartup=true`.
- **JWT secret:** required outside Development. Set `Jwt__Secret` (or `JWT_SECRET`) to 32+ random characters. Development falls back to a local-only key.
- **Payments (Paystack):** see [PAYMENTS.md](PAYMENTS.md) for keys, dashboard setup, endpoints and go-live steps. Test mode is the default; nothing is unlocked without a server-verified charge.
- **Tax rules:** see [TAX_RULES.md](TAX_RULES.md) for the versioned rule sets and items pending validation.
- **Tests:** `dotnet test backend/TaxNavi.sln`
