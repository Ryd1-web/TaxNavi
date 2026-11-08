using Microsoft.Extensions.Logging.Abstractions;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Tests;

/// <summary>Wires the real billing services to in-memory fakes and a controllable gateway and clock.</summary>
internal sealed class BillingTestHarness
{
    public FakeStore Store { get; } = new();
    public FakeGateway Gateway { get; } = new();
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));
    public SubscriptionOptions SubscriptionOptions { get; } = new();
    public static readonly CheckoutUrls Urls = new("https://app.test/checkout/callback", "https://app.test/checkout/callback");

    public SubscriptionService Subscriptions => new(Store.Subscriptions, Store.Payments, Store.Users, Gateway, TestRules.Provider,
        SubscriptionOptions, Store.UnitOfWork, Clock, NullLogger<SubscriptionService>.Instance);

    public TaxReportService Reports => new(TestRules.Engine, Store.Profiles, Store.Reports, Store.Payments, Store.UnitOfWork, Clock);

    public BillingService Billing => new(Store.Users, Store.Reports, Store.Payments, Store.Subscriptions, Store.WebhookEvents, Gateway,
        TestRules.Provider, Reports, Subscriptions, Store.UnitOfWork, Clock, NullLogger<BillingService>.Instance);

    public User AddUser(string email = "owner@example.com", string type = "Individual")
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, UserType = type, IsActive = true };
        Store.Users.Items.Add(user);
        return user;
    }

    public TaxReport AddReport(Guid? userId, string type = TaxpayerKinds.Individual)
    {
        var report = new TaxReport { Id = Guid.NewGuid(), UserId = userId, ReportType = type };
        Store.Reports.Items.Add(report);
        return report;
    }

    /// <summary>Simulates Paystack reporting the given outcome for a reference.</summary>
    public void PaystackSays(string reference, string status, decimal? naira = null, string currency = "NGN", string? reportedReference = null, string? email = null, string? plan = null)
    {
        var payment = Store.Payments.Items.FirstOrDefault(p => p.Reference == reference);
        Gateway.Verifications[reference] = new PaymentVerificationResponse
        {
            Success = status == "success" && (reportedReference ?? reference) == reference,
            Status = status,
            Reference = reportedReference ?? reference,
            AmountKobo = Money.ToKobo(naira ?? payment?.Amount ?? 0m),
            Currency = currency,
            TransactionId = 1234,
            Channel = "card",
            CustomerEmail = email ?? payment?.Email,
            PlanCode = plan,
            PaidAt = Clock.GetUtcNow().UtcDateTime
        };
    }

    public static TaxCalculationRequestDto Calculation(string type = TaxpayerKinds.Individual) => type == TaxpayerKinds.SME
        ? new TaxCalculationRequestDto
        {
            TaxYear = 2026,
            TaxpayerType = TaxpayerKinds.SME,
            BusinessStructure = BusinessStructures.LimitedCompany,
            BusinessType = "Retail",
            IncomeSources = new() { new() { Name = "Sales", Category = IncomeCategories.Business, AnnualAmount = 80_000_000m } },
            Vat = new() { AnnualTurnover = 80_000_000m }
        }
        : TestRules.Salary(5_000_000m, 2026);
}

internal sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now;
    public FakeClock(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

internal sealed class FakeGateway : IPaymentService
{
    public bool Configured { get; set; } = true;
    public bool IsConfigured => Configured;
    public bool IsTestMode => true;
    public List<PaymentRequest> Initiated { get; } = new();
    public Dictionary<string, PaymentVerificationResponse> Verifications { get; } = new();
    public Dictionary<string, ProviderSubscription> RemoteSubscriptions { get; } = new();
    public List<(string Code, string Token)> Disabled { get; } = new();
    public int VerifyCalls { get; private set; }

    public Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        Initiated.Add(request);
        return Task.FromResult(new PaymentInitiationResponse { Success = true, Reference = request.Reference, AuthorizationUrl = $"https://checkout.paystack.test/{request.Reference}" });
    }

    public Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference, CancellationToken cancellationToken = default)
    {
        VerifyCalls++;
        return Task.FromResult(Verifications.TryGetValue(reference, out var v) ? v : new PaymentVerificationResponse { Success = false, Status = "ongoing", Reference = reference });
    }

    public bool IsValidWebhookSignature(byte[] rawBody, string? signature) => false;

    public Task<ProviderSubscription?> FetchSubscriptionAsync(string subscriptionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(RemoteSubscriptions.TryGetValue(subscriptionCode, out var s) ? s : null);

    public Task<bool> DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken cancellationToken = default)
    {
        Disabled.Add((subscriptionCode, emailToken));
        if (RemoteSubscriptions.TryGetValue(subscriptionCode, out var s))
        {
            s.Status = "non-renewing";
        }
        return Task.FromResult(true);
    }

    public Task<string?> GetSubscriptionManageLinkAsync(string subscriptionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>($"https://paystack.test/manage/{subscriptionCode}");
}

internal sealed class FakeStore
{
    public FakeUsers Users { get; } = new();
    public FakeProfiles Profiles { get; } = new();
    public FakeReports Reports { get; } = new();
    public FakePayments Payments { get; } = new();
    public FakeSubscriptions Subscriptions { get; } = new();
    public FakeWebhookEvents WebhookEvents { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
}

/// <summary>Runs work directly; relational transaction behaviour is covered by the SQL Server integration tests.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Transactions { get; private set; }

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        Transactions++;
        await work(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        Transactions++;
        return await work(cancellationToken);
    }
}

internal class FakeRepo<T> : IRepository<T> where T : class
{
    public List<T> Items { get; } = new();
    private static Guid IdOf(T entity) => (Guid)typeof(T).GetProperty("Id")!.GetValue(entity)!;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(i => IdOf(i) == id));
    public Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<T>>(Items);
    public Task AddAsync(T entity, CancellationToken cancellationToken = default) { Items.Add(entity); return Task.CompletedTask; }
    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { Items.RemoveAll(i => IdOf(i) == id); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeUsers : FakeRepo<User>, IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
}

internal sealed class FakeProfiles : FakeRepo<TaxProfile>, ITaxProfileRepository
{
    public Task<IEnumerable<TaxProfile>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(p => p.UserId == userId));
}

internal sealed class FakeReports : FakeRepo<TaxReport>, ITaxReportRepository
{
    public Task<IEnumerable<TaxReport>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(r => r.UserId == userId));
    public Task<TaxReport?> GetForUserAsync(Guid reportId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(r => r.Id == reportId && r.UserId == userId));
    public Task<bool> AnyForProfileAsync(Guid taxProfileId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Any(r => r.TaxProfileId == taxProfileId));
}

internal sealed class FakePayments : FakeRepo<Payment>, IPaymentRepository
{
    public int FulfilmentWins { get; private set; }

    public Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(p => p.UserId == userId));
    public Task<Payment?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(p => p.Reference == reference));
    public Task<IEnumerable<Payment>> GetByTaxReportIdAsync(Guid taxReportId, CancellationToken cancellationToken = default) => Task.FromResult(Items.Where(p => p.TaxReportId == taxReportId).ToList().AsEnumerable());

    public Task<Payment?> GetLatestPendingAsync(string purpose, Guid? taxReportId, Guid? userId, CancellationToken cancellationToken = default) => Task.FromResult(Items
        .Where(p => p.Status == PaymentStatuses.Pending && p.Purpose == purpose)
        .Where(p => taxReportId.HasValue ? p.TaxReportId == taxReportId : p.UserId == userId)
        .OrderByDescending(p => p.CreatedAt)
        .FirstOrDefault());

    public Task<bool> TryMarkFulfilledAsync(Guid paymentId, DateTime fulfilledAt, CancellationToken cancellationToken = default)
    {
        var payment = Items.First(p => p.Id == paymentId);
        if (payment.FulfilledAt != null)
        {
            return Task.FromResult(false);
        }
        payment.FulfilledAt = fulfilledAt;
        FulfilmentWins++;
        return Task.FromResult(true);
    }

    public Task ReloadAsync(Payment payment, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class FakeSubscriptions : FakeRepo<Subscription>, ISubscriptionRepository
{
    public Task<Subscription?> GetLatestForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(s => s.UserId == userId).OrderByDescending(s => s.CurrentPeriodEnd).FirstOrDefault());

    public Task<Subscription?> GetByProviderCodeAsync(string subscriptionCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(s => s.ProviderSubscriptionCode == subscriptionCode));
}

internal sealed class FakeWebhookEvents : IPaymentWebhookEventRepository
{
    public List<PaymentWebhookEvent> Items { get; } = new();
    public Task<PaymentWebhookEvent?> GetByKeyAsync(string eventKey, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(e => e.EventKey == eventKey));

    public Task<bool> TryAddAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default)
    {
        if (Items.Any(e => e.EventKey == webhookEvent.EventKey)) return Task.FromResult(false);
        Items.Add(webhookEvent);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(PaymentWebhookEvent webhookEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PaymentWebhookEvent>> GetRetryableAsync(int maxAttempts, DateTime staleBefore, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentWebhookEvent>>(Items
            .Where(e => e.Attempts < maxAttempts && e.Status != WebhookEventStatuses.Processed)
            .Where(e => e.LastAttemptAt == null || e.LastAttemptAt < staleBefore)
            .OrderBy(e => e.ReceivedAt).Take(take).ToList());
}
