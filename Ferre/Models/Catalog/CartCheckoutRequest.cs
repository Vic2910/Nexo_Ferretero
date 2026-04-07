namespace Ferre.Models.Catalog;

public sealed class CartCheckoutRequest
{
    public List<CartCheckoutItem> Items { get; set; } = new();

    public string PaymentMethod { get; set; } = string.Empty;

    public CardPaymentData? Card { get; set; }

    public PayPalPaymentData? PayPal { get; set; }

    public CashPaymentData? Cash { get; set; }
}

public sealed class CartCheckoutItem
{
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
}

public sealed class CardPaymentData
{
    public string HolderName { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public int ExpiryMonth { get; set; }

    public int ExpiryYear { get; set; }

    public string Cvv { get; set; } = string.Empty;
}

public sealed class PayPalPaymentData
{
    public string Email { get; set; } = string.Empty;

    public string OrderId { get; set; } = string.Empty;
}

public sealed class CashPaymentData
{
    public string CustomerName { get; set; } = string.Empty;
}
