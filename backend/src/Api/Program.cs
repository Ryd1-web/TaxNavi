using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using TaxNavi.Infrastructure.Services;
using TaxNavi.Application.Services;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Validators;
using TaxNavi.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and then your token. Example: Bearer eyJhbGc..."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000", "http://localhost:3001")
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
});

// Add Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=NG-PF48LHK1\\SQLEXPRESS;Database=TaxNavi;Trusted_Connection=true;Encrypt=false;";
builder.Services.AddDbContext<TaxNaviDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("TaxNavi.Infrastructure"))
);

// Add JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? "your-super-secret-key-that-is-at-least-32-characters-long-change-in-production";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TaxNavi.ai";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TaxNavi.Users";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaxProfileRepository, TaxProfileRepository>();
builder.Services.AddScoped<ITaxReportRepository, TaxReportRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

// Register Services
builder.Services.AddScoped<ITaxCalculationService, TaxCalculationService>();
builder.Services.AddScoped<IAiTaxAdvisorService, AzureAiTaxAdvisorService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPdfService, PdfReportService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<BlobStorageService>();

// Add Validators
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<IndividualTaxAssessmentValidator>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<IndividualTaxAssessmentDto>, IndividualTaxAssessmentValidator>();
builder.Services.AddScoped<IValidator<SMEAssessmentDto>, SMEAssessmentValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger for development
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaxNavi API v1");
    c.RoutePrefix = "swagger";
});

// Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", environment = app.Environment.EnvironmentName }));
app.MapGet("/", () => Results.Redirect("/swagger"));

// CORS must come before authentication
app.UseCors("AllowReactApp");

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Run migrations or create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaxNaviDbContext>();
    if (app.Environment.IsDevelopment())
    {
        // For in-memory database, just ensure it's created
        db.Database.EnsureCreated();
    }
    else
    {
        // For SQL Server, run migrations
        db.Database.Migrate();
    }
}

app.Run();
