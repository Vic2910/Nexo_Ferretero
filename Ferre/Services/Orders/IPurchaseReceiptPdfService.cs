using Ferre.Models.Orders;

namespace Ferre.Services.Orders;

public interface IPurchaseReceiptPdfService
{
    byte[] Generate(ClientPurchaseReceipt receipt, string customerEmail, string customerName);
}
