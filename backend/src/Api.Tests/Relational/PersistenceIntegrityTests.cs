using Microsoft.EntityFrameworkCore;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Exceptions;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using Xunit;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>Constraints, concurrency and transaction behaviour against a real SQL Server database.</summary>
public class PersistenceIntegrityTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _db;

    public PersistenceIntegrityTests(PostgresFixture db) => _db = db;

    private static Payment NewPayment(Guid? reportId = null, string status = PaymentStatuses.Pending) => new()
    {
        Id = Guid.NewGuid(),
        TaxReportId = reportId,
        Purpose = PaymentPurposes.PremiumReport,
        Amount = 3_500m,
        Currency = "NGN",
        Status = status,
        Reference = $"TNV-T-{Guid.NewGuid():N}",
        Email = "guest@example.com",
        CreatedAt = DateTime.UtcNow
    };

    private async Task<TaxReport> NewReportAsync()
    {
        await using var db = _db.NewContext();
        var services = _db.Services(db, new SqlTestGateway());
        var created = await services.Reports.CreateAsync(TestData.Salary(), userId: null, guestEmail: "guest@example.com");
        return created.Entity;
    }

    [PostgresFact]
    public async Task ForeignKey_PaymentForMissingReport_IsRejected()
    {
        await using var db = _db.NewContext();
        db.Payments.Add(NewPayment(reportId: Guid.NewGuid()));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task UniqueReference_IsEnforced_AsDuplicateRecord()
    {
        var first = NewPayment();
        var second = NewPayment();
        second.Reference = first.Reference;

        await using var db = _db.NewContext();
        var repo = new PaymentRepository(db);
        await repo.AddAsync(first);
        await Assert.ThrowsAsync<DuplicateRecordException>(() => repo.AddAsync(second));
    }

    [PostgresFact]
    public async Task OnlyOneOpenCheckoutPerReport_IsEnforced()
    {
        var report = await NewReportAsync();

        await using (var db = _db.NewContext())
        {
            var repo = new PaymentRepository(db);
            await repo.AddAsync(NewPayment(report.Id, PaymentStatuses.Completed)); // history is fine
            await repo.AddAsync(NewPayment(report.Id));                              // first open checkout
        }

        await using var db2 = _db.NewContext();
        await Assert.ThrowsAsync<DuplicateRecordException>(() => new PaymentRepository(db2).AddAsync(NewPayment(report.Id)));
    }

    [PostgresFact]
    public async Task TryMarkFulfilled_UnderConcurrency_HasExactlyOneWinner()
    {
        var payment = NewPayment(status: PaymentStatuses.Completed);
        await using (var db = _db.NewContext())
        {
            await new PaymentRepository(db).AddAsync(payment);
        }

        var attempts = Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var db = _db.NewContext();
            return await new PaymentRepository(db).TryMarkFulfilledAsync(payment.Id, DateTime.UtcNow);
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(won => won));
    }

    [PostgresFact]
    public async Task StaleWrite_CannotOverwriteACompletedPayment()
    {
        var payment = NewPayment();
        await using (var setup = _db.NewContext())
        {
            await new PaymentRepository(setup).AddAsync(payment);
        }

        await using var webhookCtx = _db.NewContext();
        await using var callbackCtx = _db.NewContext();
        var viaWebhook = await webhookCtx.Payments.SingleAsync(p => p.Id == payment.Id);
        var viaCallback = await callbackCtx.Payments.SingleAsync(p => p.Id == payment.Id);

        viaWebhook.Status = PaymentStatuses.Completed;
        viaWebhook.FulfilledAt = DateTime.UtcNow;
        await new PaymentRepository(webhookCtx).UpdateAsync(viaWebhook);

        viaCallback.Status = PaymentStatuses.Cancelled; // stale "abandoned" result arriving late
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new PaymentRepository(callbackCtx).UpdateAsync(viaCallback));

        await using var check = _db.NewContext();
        var stored = await check.Payments.SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatuses.Completed, stored.Status);
        Assert.NotNull(stored.FulfilledAt);
    }

    [PostgresFact]
    public async Task UnitOfWork_RollsBackAllWrites_WhenAnyStepFails()
    {
        var profileId = Guid.NewGuid();
        await using (var db = _db.NewContext())
        {
            var uow = new EfUnitOfWork(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
            {
                await new TaxProfileRepository(db).AddAsync(new TaxProfile { Id = profileId, ProfileType = "Individual", CreatedAt = DateTime.UtcNow }, ct);
                throw new InvalidOperationException("boom");
            }));
            Assert.Empty(db.ChangeTracker.Entries()); // nothing half-applied left to save later
        }

        await using var check = _db.NewContext();
        Assert.False(await check.TaxProfiles.AnyAsync(p => p.Id == profileId));
    }

    [PostgresFact]
    public async Task FulfilmentFailure_RollsBackThePaymentStatusChange()
    {
        var gateway = new SqlTestGateway();
        string reference;
        Guid reportId;
        await using (var db = _db.NewContext())
        {
            var checkout = await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls);
            reference = checkout.Data!.Reference;
            reportId = checkout.Data.TaxReportId!.Value;
        }

        gateway.Paid(reference, 3_500m, "guest@example.com");
        await using (var db = _db.NewContext())
        {
            var services = _db.Services(db, gateway, reportRepository: new FailingReportRepository(db));
            await Assert.ThrowsAsync<InvalidOperationException>(() => services.Billing.VerifyAsync(null, reference));
        }

        await using var check = _db.NewContext();
        var payment = await check.Payments.SingleAsync(p => p.Reference == reference);
        var report = await check.TaxReports.SingleAsync(r => r.Id == reportId);
        Assert.Equal(PaymentStatuses.Pending, payment.Status); // not "Completed but undelivered"
        Assert.Null(payment.FulfilledAt);
        Assert.False(report.IsUnlocked);

        // Once the fault clears, verifying again completes everything together.
        await using var retry = _db.NewContext();
        Assert.Equal(BillingOutcome.Ok, (await _db.Services(retry, gateway).Billing.VerifyAsync(null, reference)).Outcome);
    }

    private sealed class FailingReportRepository : TaxReportRepository
    {
        public FailingReportRepository(TaxNaviDbContext context) : base(context) { }

        public override Task UpdateAsync(TaxReport entity, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated failure while unlocking the report");
    }
}
