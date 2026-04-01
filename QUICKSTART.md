# Quick Start Guide - TaxNavi.ai

## Prerequisites
- .NET 8 SDK
- Node.js 20+
- SQL Server (2022) or LocalDB
- Git
- Docker (optional)

## 5-Minute Setup

### Option 1: Local Development (No Docker)

#### Backend Setup
```bash
cd backend

# Restore packages
dotnet restore

# Update database (with LocalDB)
cd src/Infrastructure
dotnet ef database update --startup-project ../Api/TaxNavi.Api.csproj

# Run API
cd ../Api
dotnet run
```
✅ API running on http://localhost:5000

#### Frontend Setup
```bash
cd frontend

# Install dependencies
npm install

# Run development server
npm run dev
```
✅ Frontend running on http://localhost:3000

#### Access Application
- **Frontend**: http://localhost:3000
- **API Swagger**: http://localhost:5000/swagger
- **Default User**: (Create one via registration)

---

### Option 2: Docker Compose (Complete Stack)

```bash
# From project root
docker-compose up --build

# Wait for all services to start (2-3 minutes)
```

✅ All services running:
- Frontend: http://localhost:3000
- API: http://localhost:5000
- Database: localhost:1433

---

## First Steps

### 1. Register an Account
1. Go to http://localhost:3000
2. Click "Register"
3. Fill in details
4. Select "Individual" or "SME"
5. Click Register

### 2. Perform Tax Assessment
#### Individual
1. Go to Dashboard → Individual Tax Assessment
2. Enter income/expenses
3. Click "Calculate Tax"
4. View results with AI insights

#### SME
1. Go to Dashboard → SME Tax Assessment
2. Enter business details
3. Click "Calculate Tax Obligations"
4. Review compliance roadmap

### 3. Download Report
1. Go to Reports
2. Click "View Details" on any report
3. Click "Download PDF"

---

## Environment Configuration

### Backend
Create `backend/.env.local`:
```
DATABASE_CONNECTION_STRING=Data Source=(localdb)\mssqllocaldb;Initial Catalog=TaxNavi;Integrated Security=true;
JWT_SECRET=your-32-char-secret-key-here
```

### Frontend
Create `frontend/.env.local`:
```
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

---

## Testing Accounts

### Test Individual
- Email: `individual@test.com`
- Password: `TestPass123`
- Income: ₦5,500,000

### Test SME
- Email: `sme@test.com`
- Password: `TestPass123`
- Turnover: ₦50,000,000

---

## Common Commands

### Backend
```bash
# Build
dotnet build

# Run tests
dotnet test

# Create migration
cd src/Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Api/TaxNavi.Api.csproj

# Update database
dotnet ef database update --startup-project ../Api/TaxNavi.Api.csproj
```

### Frontend
```bash
# Build for production
npm run build

# Start production server
npm start

# Run tests
npm test

# Lint code
npm run lint
```

---

## Troubleshooting

### Backend won't start
```bash
# Check if port 5000 is available
# Kill process on port 5000: netstat -ano | findstr :5000

# Check database connection
# Ensure SqlLocalDb is running: sqllocaldb start
```

### Frontend won't connect to API
```bash
# Check API is running: http://localhost:5000/swagger
# Check NEXT_PUBLIC_API_URL in .env.local
# Browser console errors (F12 → Console)
```

### Database migration fails
```bash
# Reset database
dotnet ef database drop --startup-project ../Api/TaxNavi.Api.csproj

# Reapply migrations
dotnet ef database update --startup-project ../Api/TaxNavi.Api.csproj
```

### Port already in use
```bash
# Change API port in Properties/launchSettings.json
# Change frontend port: npm run dev -- -p 3001
```

---

## Azure Configuration (Optional)

To use Azure services:

1. **Create Azure Account**: https://azure.microsoft.com
2. **Create Resource Group**
3. **Create Azure OpenAI Resource**
4. **Create Azure Blob Storage Account**

Set environment variables:
```
AZURE_OPENAI_API_KEY=your-key
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_BLOB_STORAGE_CONNECTION_STRING=your-connection-string
```

---

## Production Deployment

### Azure App Service
```bash
# Publish backend
cd backend/src/Api
dotnet publish -c Release -o publish

# Create App Service and deploy
az webapp deployment source config-zip --resource-group taxnavi-rg \
  --name taxnavi-api --src publish.zip
```

### Vercel (Frontend)
```bash
npm i -g vercel
vercel deploy --prod
```

---

## Support

- **Documentation**: See README.md files
- **API Docs**: http://localhost:5000/swagger
- **Issues**: Create GitHub issue

---

## Next Steps

1. ✅ Run application locally
2. 📖 Read full documentation
3. 🔧 Configure Azure services (optional)
4. 📝 Customize tax rules for your region
5. 🚀 Deploy to production

Enjoy using TaxNavi.ai! 🎉
