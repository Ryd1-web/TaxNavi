using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using Xunit;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>
/// A throwaway PostgreSQL database built from the real EF migrations, dropped afterwards.
/// Defaults to the local Docker container on port 5433; override with TAXNAVI_TEST_POSTGRES
/// (a connection string without a database name).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string DefaultServer = "Host=localhost;Port=5433;Username=taxnavi;Password=taxnavi_dev";

    public static string Server => Environment.GetEnvironmentVariable("TAXNAVI_TEST_POSTGRES") ?? DefaultServer;

    public string ConnectionString { get; }

    public PostgresFixture()
    {
        NpgsqlSettings.Configure();
        var builder = new NpgsqlConnectionStringBuilder(Server) { Database = $"taxnavi_test_{Guid.NewGuid():N}", Timeout = 30 };
        ConnectionString = builder.ConnectionString;
    }

    public TaxNaviDbContext NewContext() => new(new DbContextOptionsBuilder<TaxNaviDbContext>()
        .UseNpgsql(ConnectionString, b => b.MigrationsAssembly("TaxNavi.Infrastructure"))
        .Options);

    public async Task InitializeAsync()
    {
        if (!PostgresAvailable.IsAvailable)
        {
            return;
        }

        await using var db = NewContext();
        await db.Database.MigrateAsync(); // applies the real migration chain
    }

    public async Task DisposeAsync()
    {
        if (!PostgresAvailable.IsAvailable)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools(); // release pooled connections so the database can be dropped
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

/// <summary>Skips relational tests (with a reason) when no PostgreSQL server is reachable.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresAvailable.IsAvailable)
        {
            Skip = "PostgreSQL not available. Start one (docker run -d -p 5433:5432 -e POSTGRES_USER=taxnavi -e POSTGRES_PASSWORD=taxnavi_dev postgres:16-alpine) or set TAXNAVI_TEST_POSTGRES.";
        }
    }
}

internal static class PostgresAvailable
{
    private static readonly Lazy<bool> Probe = new(() =>
    {
        try
        {
            NpgsqlSettings.Configure();
            using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(PostgresFixture.Server) { Database = "postgres", Timeout = 10 }.ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception ex) when (ex is NpgsqlException or SocketException or TimeoutException)
        {
            return false;
        }
    });

    public static bool IsAvailable => Probe.Value;
}
