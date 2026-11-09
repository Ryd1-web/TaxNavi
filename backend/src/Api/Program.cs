using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Application.Validators;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using TaxNavi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Hosts such as Render assign the listening port through PORT.
var hostPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(hostPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort}");
}

// Behind a TLS-terminating proxy (Render), trust X-Forwarded-For/Proto so HTTPS redirection doesn't loop
// and rate limits apply per real client IP instead of per proxy.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                               Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear(); // Render's proxy addresses are not fixed
    options.KnownProxies.Clear();
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Consistent { success, message, errors } shape for validation failures.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            var first = errors.Values.SelectMany(v => v).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Some details need attention.";
            return new BadRequestObjectResult(new { success = false, message = first, code = "VALIDATION_FAILED", errors });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:3001" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader().WithExposedHeaders("Content-Disposition"));
});

// Rate limiting (per client IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 10), Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("calculate", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:CalculatePerMinute", 60), Window = TimeSpan.FromMinutes(1) }));
    var checkoutLimit = builder.Configuration.GetValue("RateLimits:CheckoutPerMinute", 30);
    options.AddPolicy("checkout", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = checkoutLimit, Window = TimeSpan.FromMinutes(1) }));
});

// Database (PostgreSQL). Render and similar hosts provide DATABASE_URL as a postgres:// URI.
NpgsqlSettings.Configure();
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(rawConnectionString))
{
    rawConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? string.Empty;
}
var connectionString = NpgsqlSettings.NormalizeConnectionString(rawConnectionString);
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
var applyDatabaseSetupOnStartup = builder.Configuration.GetValue<bool>("DatabaseSetup:ApplyOnStartup");
builder.Services.AddDbContext<TaxNaviDbContext>(options =>
{
    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("TaxNavi");
        return;
    }

    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("TaxNavi.Infrastructure"));
});

// Tax rules (versioned, per tax year)
var taxRules = builder.Configuration.GetSection("TaxRules").Get<TaxRulesConfiguration>() ?? new TaxRulesConfiguration();
if (taxRules.RuleSets.Count == 0 || taxRules.RuleSets.All(set => !set.AppliesTo(taxRules.DefaultTaxYear)))
{
    throw new InvalidOperationException("TaxRules configuration must include a rule set for TaxRules:DefaultTaxYear.");
}

var pricing = taxRules.Monetization;
if (pricing.PayPerReportPrice <= 0 || pricing.SmeSubscriptionPrice <= 0 || pricing.ReportPrices.Values.Any(price => price <= 0) ||
    !string.Equals(pricing.Currency, "NGN", StringComparison.Ordinal))
{
    throw new InvalidOperationException("TaxRules:Monetization prices must be positive naira amounts and Currency must be NGN.");
}

// Paystack: secret key from user secrets / environment only (PAYSTACK_SECRET_KEY is accepted as a fallback).
var paystackOptions = builder.Configuration.GetSection("Paystack").Get<PaystackOptions>() ?? new PaystackOptions();
if (string.IsNullOrWhiteSpace(paystackOptions.SecretKey))
{
    paystackOptions.SecretKey = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
}
builder.Services.AddSingleton(paystackOptions);
builder.Services.AddSingleton(new SubscriptionOptions { SmePlanCode = paystackOptions.SmePlanCode });

// JWT
var jwt = JwtSettings.FromConfiguration(builder.Configuration);
if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32 || jwt.Secret.Contains("change-in-production", StringComparison.OrdinalIgnoreCase))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Set a strong Jwt:Secret (32+ characters) via configuration or the JWT_SECRET environment variable.");
    }

    jwt.Secret = "development-only-signing-key-do-not-use-in-production-0001";
}
builder.Services.AddSingleton(jwt);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaxProfileRepository, TaxProfileRepository>();
builder.Services.AddScoped<ITaxReportRepository, TaxReportRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPaymentWebhookEventRepository, PaymentWebhookEventRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITaxRuleProvider>(new ConfiguredTaxRuleProvider(taxRules));
builder.Services.AddScoped<ITaxCalculationEngine, NigerianTaxCalculationEngine>();
builder.Services.AddScoped<ITaxAccessControlService, TaxAccessControlService>();
builder.Services.AddScoped<ITaxAssistantService, GroundedTaxAssistantService>();
builder.Services.AddScoped<ITaxReportService, TaxReportService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPdfService, PdfReportService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddHttpClient<IPaymentService, PaystackPaymentService>(client =>
{
    client.BaseAddress = new Uri(paystackOptions.BaseUrl.EndsWith('/') ? paystackOptions.BaseUrl : paystackOptions.BaseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(20);
});

// Validators
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<TaxCalculationRequestValidator>();

if (builder.Configuration.GetValue("Webhooks:RetryEnabled", true))
{
    builder.Services.AddHostedService<TaxNavi.Api.BackgroundServices.WebhookRetryWorker>();
}

var app = builder.Build();

app.UseForwardedHeaders(); // must run before anything that reads the scheme or client IP

using (var scope = app.Services.CreateScope())
{
    var gateway = scope.ServiceProvider.GetRequiredService<IPaymentService>();
    app.Logger.LogInformation("Payments: {Mode}; SME plan billing: {Billing}",
        !gateway.IsConfigured ? "disabled (no Paystack key)" : gateway.IsTestMode ? "Paystack TEST mode" : "Paystack LIVE mode",
        string.IsNullOrWhiteSpace(paystackOptions.SmePlanCode) ? "one-off monthly pass" : "recurring Paystack plan");
}

// Never leak exception details to clients.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is UnsupportedTaxYearException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { success = false, message = error.Message, code = "UNSUPPORTED_TAX_YEAR" });
        return;
    }

    app.Logger.LogError(error, "Unhandled exception for {Path}", context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { success = false, message = "Something went wrong on our side. Please try again." });
}));

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TaxNavi API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (applyDatabaseSetupOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TaxNaviDbContext>();
    if (useInMemoryDatabase)
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }
}
else if (!useInMemoryDatabase)
{
    // Schema drift causes opaque 500s on save, so say so loudly at startup.
    try
    {
        using var scope = app.Services.CreateScope();
        var pending = scope.ServiceProvider.GetRequiredService<TaxNaviDbContext>().Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            app.Logger.LogWarning(
                "Database is missing {Count} migration(s): {Migrations}. Run 'dotnet ef database update --project src/Infrastructure --startup-project src/Api' or set DatabaseSetup:ApplyOnStartup=true.",
                pending.Count, string.Join(", ", pending));
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not check database migrations at startup.");
    }
}

app.Run();

public partial class Program;
