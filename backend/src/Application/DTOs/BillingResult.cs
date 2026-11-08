namespace TaxNavi.Application.DTOs;

public class BillingResult<T>
{
    public BillingOutcome Outcome { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static BillingResult<T> Ok(T data, string message = "") => new() { Outcome = BillingOutcome.Ok, Data = data, Message = message };
    public static BillingResult<T> Fail(BillingOutcome outcome, string message, T? data = default) => new() { Outcome = outcome, Message = message, Data = data };
}
