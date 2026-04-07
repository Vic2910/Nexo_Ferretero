namespace Ferre.Options;

public sealed class PayPalSettings
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api-m.sandbox.paypal.com";
    public string CurrencyCode { get; init; } = "USD";
}
