using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using Xunit;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>
/// A throwaway SQL Server database (LocalDB by default) built from the real EF migrations, dropped afterwards.
/// Override the server with TAXNAVI_TEST_SQLSERVER (a connection string without a database name).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true;";

    public string ConnectionString { get; }

    public SqlServerFixture()
    {
        var server = Environment.GetEnvironmentVariable("TAXNAVI_TEST_SQLSERVER") ?? DefaultServer;
        var builder = new SqlConnectionStringBuilder(server) { InitialCatalog = $"TaxNavi_Test_{Guid.NewGuid():N}", ConnectTimeout = 60 };
        ConnectionString = builder.ConnectionString;
    }

    public TaxNaviDbContext NewContext() => new(new DbContextOptionsBuilder<TaxNaviDbContext>()
        .UseSqlServer(ConnectionString, b => b.MigrationsAssembly("TaxNavi.Infrastructure"))
        .Options);

    public async Task InitializeAsync()
    {
        if (!SqlServerAvailable.IsAvailable)
        {
            return;
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync(); // applies the real migration chain, including data-fix steps
    }

    public async Task DisposeAsync()
    {
        if (!SqlServerAvailable.IsAvailable)
        {
            return;
        }

        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>Real services over real repositories on one DbContext — the same wiring as the API.</summary>
    public TestServices Services(TaxNaviDbContext db, SqlTestGateway gateway, string? planCode = null, TaxReportRepository? reportRepository = null)
    {
        var users = new UserRepository(db);
        var profiles = new TaxProfileRepository(db);
        var reports = reportRepository ?? new TaxReportRepository(db);
        var payments = new PaymentRepository(db);
        var subscriptions = new SubscriptionRepository(db);
        var events = new PaymentWebhookEventRepository(db);
        var unitOfWork = new EfUnitOfWork(db);
        var rules = Rules.Provider;
        var engine = new NigerianTaxCalculationEngine(rules);
        var subscriptionService = new SubscriptionService(subscriptions, payments, users, gateway, rules,
            new SubscriptionOptions { SmePlanCode = planCode }, unitOfWork, TimeProvider.System, NullLogger<SubscriptionService>.Instance);
        var reportService = new TaxReportService(engine, profiles, reports, payments, unitOfWork, TimeProvider.System);
        var billing = new BillingService(users, reports, payments, subscriptions, events, gateway, rules, reportService,
            subscriptionService, unitOfWork, TimeProvider.System, NullLogger<BillingService>.Instance);
        return new TestServices(billing, subscriptionService, reportService, users, reports, payments, unitOfWork);
    }
}

public record TestServices(
    BillingService Billing,
    SubscriptionService Subscriptions,
    TaxReportService Reports,
    UserRepository Users,
    TaxReportRepository ReportRepository,
    PaymentRepository PaymentRepository,
    EfUnitOfWork UnitOfWork);

/// <summary>Loads the shipped tax/pricing configuration so tests use the real prices.</summary>
internal static class Rules
{
    private static readonly Lazy<ITaxRuleProvider> Lazy = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "Api", "appsettings.json"))) dir = dir.Parent;
            path = Path.Combine(dir!.FullName, "src", "Api", "appsettings.json");
        }
        var config = new ConfigurationBuilder().AddJsonFile(path).Build().GetSection("TaxRules").Get<TaxRulesConfiguration>()!;
        return new ConfiguredTaxRuleProvider(config);
    });

    public static ITaxRuleProvider Provider => Lazy.Value;
}

/// <summary>Skips relational tests (with a reason) when no SQL Server instance is reachable.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerAvailable.IsAvailable)
        {
            Skip = "SQL Server (LocalDB) not available. Set TAXNAVI_TEST_SQLSERVER to run relational tests.";
        }
    }
}

internal static class SqlServerAvailable
{
    private static readonly Lazy<bool> Probe = new(() =>
    {
        try
        {
            var server = Environment.GetEnvironmentVariable("TAXNAVI_TEST_SQLSERVER") ?? SqlServerFixture.DefaultServer;
            using var connection = new SqlConnection(new SqlConnectionStringBuilder(server) { InitialCatalog = "master", ConnectTimeout = 60 }.ConnectionString);
            connection.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    });

    public static bool IsAvailable => Probe.Value;
}
