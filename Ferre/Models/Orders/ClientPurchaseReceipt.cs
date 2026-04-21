namespace Ferre.Models.Orders;

public sealed class ClientPurchaseReceipt
{
    public Guid Id { get; set; }

    public string UserEmail { get; set; } = string.Empty;

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAtUtc { get; set; }

    public DateTime? DeliveredAtUtc { get; set; }

    public decimal Total { get; set; }

    public List<ClientPurchaseReceiptLine> Lines { get; set; } = new();
}

public sealed class ClientPurchaseReceiptLine
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
