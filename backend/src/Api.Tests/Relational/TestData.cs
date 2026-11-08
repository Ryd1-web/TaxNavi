using System.Text.Json;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Api.Tests.Relational;

internal static class TestData
{
    public static readonly CheckoutUrls Urls = new("https://app.test/checkout/callback", "https://app.test/checkout/callback");

    public static TaxCalculationRequestDto Salary(decimal annual = 5_000_000m) => new()
    {
        TaxYear = 2026,
        TaxpayerType = TaxpayerKinds.Individual,
        IncomeSources = new() { new() { Name = "Salary", Category = IncomeCategories.Employment, AnnualAmount = annual } }
    };

    public static CheckoutRequestDto GuestCheckout(string email = "guest@example.com") => new()
    {
        Purpose = PaymentPurposes.PremiumReport,
        Email = email,
        Calculation = Salary()
    };

    public static string Event(string name, object data) => JsonSerializer.Serialize(new { @event = name, data });

    public static User NewUser(string type = "Individual") => new()
    {
        Id = Guid.NewGuid(),
        Email = $"user{Guid.NewGuid():N}@example.com",
        PasswordHash = "x",
        FirstName = "Test",
        LastName = "User",
        UserType = type,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
}
