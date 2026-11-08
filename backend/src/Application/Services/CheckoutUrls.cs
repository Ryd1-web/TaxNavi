namespace TaxNavi.Application.Services;

/// <summary>Where Paystack sends the customer after paying (callback) or cancelling (cancel).</summary>
public record CheckoutUrls(string CallbackUrl, string CancelUrl);
