# Backend Development Guide

## Project Structure

### Clean Architecture Layers

1. **Domain Layer** (`TaxNavi.Domain`)
   - Pure business logic
   - No external dependencies
   - Contains entities and interfaces
   - `Entities/`: Domain models (User, TaxProfile, TaxReport, Payment)
   - `Interfaces/`: Service interfaces and contracts

2. **Application Layer** (`TaxNavi.Application`)
   - Use cases and business logic
   - Depends only on Domain
   - `Services/`: Tax calculation logic
   - `DTOs/`: Data Transfer Objects
   - `Validators/`: FluentValidation rules
   - `Handlers/`: Future CQRS command/query handlers

3. **Infrastructure Layer** (`TaxNavi.Infrastructure`)
   - Data access and external services
   - Repository pattern
   - `Data/`: DbContext and database config
   - `Repositories/`: Data access implementation
   - `Services/`: Azure AI, PDF generation, Payment, Auth
   - `Migrations/`: EF Core migrations

4. **API Layer** (`TaxNavi.Api`)
   - HTTP endpoints
   - Dependency injection
   - Middleware
   - `Controllers/`: REST endpoints
   - `Program.cs/`: Service configuration
   - `Middleware/`: Custom middleware

## Key Features Implementation

### 1. Tax Calculation Engine

**Location**: `src/Application/Services/TaxCalculationService.cs`

Implements Nigerian tax calculation including:
- PIT calculation with consolidated relief
- Application of tax bands
- SME VAT and WHT calculation
- Deduction optimization

### 2. AI Integration

**Location**: `src/Infrastructure/Services/AzureServices.cs`

Uses Azure OpenAI to generate:
- Human-readable tax summaries
- Personalized tax savings advice
- SME compliance roadmaps

Fallback mode available without Azure subscription.

### 3. PDF Generation

**Location**: `src/Infrastructure/Services/PdfAndPaymentServices.cs`

Uses QuestPDF to create:
- Professional tax reports
- Formatted tables and summaries
- Charts and graphs (extensible)
- Uploads to Azure Blob Storage

### 4. Authentication

**Location**: `src/Infrastructure/Services/AuthenticationService.cs`

- JWT token generation
- BCrypt password hashing
- Refresh token support
- 24-hour token expiration

### 5. Database

**Location**: `src/Infrastructure/Data/TaxNaviDbContext.cs`

Tables:
- `Users`: User accounts
- `TaxProfiles`: Individual/SME tax data
- `TaxReports`: Generated tax reports
- `Payments`: Payment tracking

## API Endpoints

### Authentication

```
POST /api/auth/register      - Create new account
POST /api/auth/login         - Login to account
POST /api/auth/refresh-token - Refresh JWT token
```

### Tax Assessment

```
POST /api/taxassessment/individual  - Calculate individual tax
POST /api/taxassessment/sme         - Calculate SME tax obligations
```

### Reports

```
GET  /api/reports            - List user reports
GET  /api/reports/{id}       - Get specific report
```

### Payments

```
POST /api/payments/initiate  - Initiate payment
POST /api/payments/verify    - Verify payment
```

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=...;Initial Catalog=TaxNavi;..."
  },
  "Jwt": {
    "Secret": "long-secret-key-min-32-chars",
    "Issuer": "TaxNavi.ai",
    "Audience": "TaxNavi.Users",
    "ExpirationMinutes": 1440
  },
  "Azure": {
    "BlobStorage": {
      "ConnectionString": "DefaultEndpointsProtocol=https;..."
    },
    "OpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key"
    }
  }
}
```

### Environment Variables

```
JWT_SECRET                              - JWT secret key
AZURE_OPENAI_API_KEY                   - Azure OpenAI API key
AZURE_OPENAI_ENDPOINT                  - Azure OpenAI endpoint
AZURE_BLOB_STORAGE_CONNECTION_STRING   - Azure Blob Storage connection
PAYSTACK_SECRET_KEY                    - Paystack secret key
```

## Database Migrations

### Create Migration

```bash
cd src/Infrastructure
dotnet ef migrations add MigrationName --startup-project ../Api/TaxNavi.Api.csproj
```

### Apply Migration

```bash
dotnet ef database update --startup-project ../Api/TaxNavi.Api.csproj
```

### Remove Last Migration

```bash
dotnet ef migrations remove --startup-project ../Api/TaxNavi.Api.csproj
```

## Testing

### Unit Tests Example

```csharp
[TestClass]
public class TaxCalculationServiceTests
{
    private ITaxCalculationService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = new TaxCalculationService();
    }

    [TestMethod]
    public async Task CalculateIndividualTax_WithValidInput_ReturnsCorrectResult()
    {
        // Arrange
        var input = new IndividualTaxInput { MonthlySalary = 500000 };

        // Act
        var result = await _service.CalculateIndividualTaxAsync(input);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.TaxLiability > 0);
    }
}
```

## Debugging

### Visual Studio Debugging

1. Set breakpoint in code
2. Press F5 to start debugging
3. Navigate to trigger the breakpoint
4. Use Debug window to inspect variables

### IIS Express vs Kestrel

- **Kestrel**: Built-in ASP.NET Core server
  ```bash
  dotnet run
  ```

- **IIS Express**: Windows-only, through Visual Studio

## Performance Optimization

1. **Database Queries**: Use `.ToListAsync()` for async operations
2. **Caching**: Implement caching for calculated values
3. **Pagination**: Implement for large result sets
4. **Compression**: Enable response compression
5. **Lazy Loading**: Use `Include()` to avoid N+1 queries

## Common Issues & Solutions

### Issue: Migration fails
```
Solution: Ensure correct connection string and SQL Server is running
```

### Issue: Azure OpenAI returns NULL
```
Solution: Check API key, endpoint, and deployment name
```

### Issue: CORS errors
```
Solution: Configure CORS policy in Program.cs for frontend domain
```

### Issue: JWT invalid
```
Solution: Verify JWT_SECRET matches between generation and validation
```

## Deployment Checklist

- [ ] Change JWT secret in production
- [ ] Configure Azure connection strings
- [ ] Set up Azure SQL database
- [ ] Configure Azure Blob Storage
- [ ] Set up Azure OpenAI (optional)
- [ ] Enable HTTPS
- [ ] Configure CORS for production domain
- [ ] Set up logging and monitoring
- [ ] Test all endpoints
- [ ] Configure backups and disaster recovery

## Future Enhancements

1. Add caching layer (Redis)
2. Implement rate limiting
3. Add comprehensive audit logging
4. Implement CQRS pattern
5. Add event sourcing
6. Create background job processing
7. Add real Paystack integration
8. Implement receipt OCR
9. Add multi-language support
10. Create admin dashboard

---

For more information, see the main [README.md](../README.md)
