using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ferre.Models.Orders;

namespace Ferre.Services.Orders;

public sealed class PurchaseReceiptPdfService : IPurchaseReceiptPdfService
{
    private const double ThermalPageWidth = 226.77d;

    public byte[] Generate(ClientPurchaseReceipt receipt, string customerEmail, string customerName)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var pageHeight = CalculatePageHeight(receipt);
        var contentStream = BuildContentStream(receipt, customerEmail, customerName, pageHeight);
        return BuildSinglePagePdf(contentStream, ThermalPageWidth, pageHeight);
    }

    private static string BuildContentStream(ClientPurchaseReceipt receipt, string customerEmail, string customerName, double pageHeight)
    {
        var pageWidth = ThermalPageWidth;
        var ticketWidth = pageWidth - 16d;
        var ticketHeight = pageHeight - 16d;
        var ticketX = 8d;
        var ticketY = 8d;
        var left = ticketX + 10d;
        var right = ticketX + ticketWidth - 10d;

        var customerNameText = string.IsNullOrWhiteSpace(customerName) ? "Cliente" : customerName.Trim();
        var customerEmailText = string.IsNullOrWhiteSpace(customerEmail) ? "cliente@nexoferretero.local" : customerEmail.Trim();
        var paymentMethodText = NormalizePaymentMethod(receipt.PaymentMethod);
        var statusText = NormalizeStatus(receipt.Status);

        var builder = new StringBuilder();
        builder.AppendLine("q 0.88 0.88 0.88 rg");
        builder.AppendLine($"{FormatNumber(ticketX + 4)} {FormatNumber(ticketY - 4)} {FormatNumber(ticketWidth)} {FormatNumber(ticketHeight)} re f Q");
        builder.AppendLine("q 1 1 1 rg");
        builder.AppendLine($"{FormatNumber(ticketX)} {FormatNumber(ticketY)} {FormatNumber(ticketWidth)} {FormatNumber(ticketHeight)} re f Q");
        builder.AppendLine("q 0.80 0.80 0.80 RG 1 w");
        builder.AppendLine($"{FormatNumber(ticketX)} {FormatNumber(ticketY)} {FormatNumber(ticketWidth)} {FormatNumber(ticketHeight)} re S Q");

        var currentY = ticketY + ticketHeight - 26d;
        AppendText(builder, "NEXO FERRETERO", 13, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center, "F2");
        currentY -= 16d;
        AppendText(builder, "NEXOFERRETERO.COM", 9.5, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);
        currentY -= 14d;
        AppendText(builder, "Quito - Ecuador", 8.5, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);
        currentY -= 14d;
        AppendText(builder, "ventas@nexoferretero.com", 8.5, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);

        currentY -= 16d;
        AppendHorizontalLine(builder, left, right, currentY);
        currentY -= 17d;

        AppendText(builder, $"Ticket: {receipt.ReceiptNumber}", 9.5, left, currentY, TextAlign.Left);
        currentY -= 14d;
        AppendText(builder, $"Fecha: {receipt.CreatedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}", 9.5, left, currentY, TextAlign.Left);
        currentY -= 14d;
        AppendText(builder, $"Cliente: {customerNameText}", 9.5, left, currentY, TextAlign.Left);
        currentY -= 14d;
        AppendText(builder, $"Correo: {customerEmailText}", 9.5, left, currentY, TextAlign.Left);
        currentY -= 14d;
        AppendText(builder, $"Metodo: {paymentMethodText}", 9.5, left, currentY, TextAlign.Left);
        currentY -= 14d;
        AppendText(builder, $"Estado: {statusText}", 9.5, left, currentY, TextAlign.Left);

        currentY -= 14d;
        AppendHorizontalLine(builder, left, right, currentY);
        currentY -= 15d;

        var quantityX = ticketX + 128d;
        var priceX = ticketX + 151d;
        var totalX = ticketX + 183d;
        AppendText(builder, "Articulo", 9, left, currentY, TextAlign.Left, "F2");
        AppendText(builder, "Ud", 9, quantityX, currentY, TextAlign.Left, "F2");
        AppendText(builder, "P", 9, priceX, currentY, TextAlign.Left, "F2");
        AppendText(builder, "T", 9, totalX, currentY, TextAlign.Left, "F2");

        currentY -= 8d;
        AppendHorizontalLine(builder, left, right, currentY);
        currentY -= 14d;

        var visibleItems = 0;
        foreach (var line in receipt.Lines)
        {
            if (currentY <= ticketY + 90d)
            {
                AppendText(builder, "...", 9, left, currentY, TextAlign.Left);
                currentY -= 14d;
                break;
            }

            var productName = Truncate(line.ProductName, 16);
            AppendText(builder, productName, 9, left, currentY, TextAlign.Left);
            AppendText(builder, line.Quantity.ToString(CultureInfo.InvariantCulture), 9, quantityX, currentY, TextAlign.Left);
            AppendText(builder, FormatMoney(line.UnitPrice), 9, priceX, currentY, TextAlign.Left);
            AppendText(builder, FormatMoney(line.LineTotal), 9, totalX, currentY, TextAlign.Left);
            currentY -= 14d;
            visibleItems++;
        }

        if (visibleItems == 0)
        {
            AppendText(builder, "Sin articulos", 9, left, currentY, TextAlign.Left);
            currentY -= 14d;
        }

        currentY -= 6d;
        AppendHorizontalLine(builder, left, right, currentY);
        currentY -= 18d;

        var subtotal = receipt.Lines.Sum(x => x.LineTotal);
        AppendText(builder, "Subtotal", 10, left, currentY, TextAlign.Left, "F2");
        AppendText(builder, FormatMoney(subtotal), 10, right, currentY, TextAlign.Right);
        currentY -= 15d;
        AppendText(builder, "Total", 12, left, currentY, TextAlign.Left, "F2");
        AppendText(builder, FormatMoney(receipt.Total), 12, right, currentY, TextAlign.Right, "F2");

        currentY -= 18d;
        var qrSize = 60d;
        var qrX = ticketX + ((ticketWidth - qrSize) / 2d);
        var qrY = Math.Max(ticketY + 40d, currentY - qrSize);
        AppendPseudoQr(builder, $"{receipt.ReceiptNumber}|{receipt.Total:0.00}|{receipt.CreatedAtUtc:yyyyMMddHHmm}", qrX, qrY, qrSize);

        currentY = qrY - 14d;
        AppendText(builder, "Escanear para validar ticket", 8, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);
        currentY -= 16d;
        AppendText(builder, "Gracias por su compra", 9.5, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);
        currentY -= 12d;
        AppendText(builder, "Conserve este comprobante", 9, ticketX + (ticketWidth / 2d), currentY, TextAlign.Center);

        return builder.ToString();
    }

    private static void AppendHorizontalLine(StringBuilder builder, double x1, double x2, double y)
    {
        builder.AppendLine("q 0.7 0.7 0.7 RG 0.8 w");
        builder.AppendLine($"{FormatNumber(x1)} {FormatNumber(y)} m {FormatNumber(x2)} {FormatNumber(y)} l S Q");
    }

    private static void AppendText(StringBuilder builder, string text, double fontSize, double x, double y, TextAlign align, string fontName = "F1")
    {
        var normalized = NormalizeText(text);
        var safeText = EscapePdfText(normalized);
        var estimatedWidth = normalized.Length * fontSize * 0.50d;
        var drawX = align switch
        {
            TextAlign.Center => x - (estimatedWidth / 2d),
            TextAlign.Right => x - estimatedWidth,
            _ => x
        };

        builder.AppendLine("BT");
        builder.AppendLine($"/{fontName} {FormatNumber(fontSize)} Tf");
        builder.AppendLine($"{FormatNumber(drawX)} {FormatNumber(y)} Td");
        builder.AppendLine($"({safeText}) Tj");
        builder.AppendLine("ET");
    }

    private static byte[] BuildSinglePagePdf(string contentStream, double pageWidth, double pageHeight)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {FormatNumber(pageWidth)} {FormatNumber(pageHeight)}] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.WriteLine("%PDF-1.4");
        writer.Flush();

        var offsets = new List<long> { 0 };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            writer.WriteLine($"{index + 1} 0 obj");
            writer.WriteLine(objects[index]);
            writer.WriteLine("endobj");
            writer.Flush();
        }

        var xrefPosition = stream.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {objects.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");

        for (var index = 1; index < offsets.Count; index++)
        {
            writer.WriteLine($"{offsets[index]:0000000000} 00000 n ");
        }

        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(xrefPosition);
        writer.Write("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string NormalizeStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "cancelado" or "cancelada" or "cancelados" or "canceladas" or "canceled" or "cancelled" => "Cancelado",
            "pendiente" or "pending" => "Pendiente",
            "entregado" or "delivered" => "Entregado",
            _ => "Pagado"
        };
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "efectivo" => "Efectivo",
            "tarjeta" => "Tarjeta",
            "paypal" => "PayPal",
            _ => string.IsNullOrWhiteSpace(value) ? "No definido" : value.Trim()
        };
    }

    private static string Truncate(string? value, int maxLength)
    {
        var text = NormalizeText(value ?? string.Empty);
        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength - 1), "…");
    }

    private static string FormatMoney(decimal amount)
    {
        return $"{amount.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double CalculatePageHeight(ClientPurchaseReceipt receipt)
    {
        var estimated = 440d + (receipt.Lines.Count * 14d);
        return Math.Clamp(estimated, 520d, 1050d);
    }

    private static void AppendPseudoQr(StringBuilder builder, string seed, double x, double y, double size)
    {
        var modules = 21;
        var moduleSize = size / modules;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

        for (var row = 0; row < modules; row++)
        {
            for (var col = 0; col < modules; col++)
            {
                if (!IsQrDark(row, col, bytes))
                {
                    continue;
                }

                var cellX = x + (col * moduleSize);
                var cellY = y + ((modules - row - 1) * moduleSize);
                builder.AppendLine($"q 0 0 0 rg {FormatNumber(cellX)} {FormatNumber(cellY)} {FormatNumber(moduleSize)} {FormatNumber(moduleSize)} re f Q");
            }
        }
    }

    private static bool IsQrDark(int row, int col, byte[] bytes)
    {
        if (InFinder(row, col, 0, 0) || InFinder(row, col, 0, 14) || InFinder(row, col, 14, 0))
        {
            return true;
        }

        var index = (row * 21 + col) % bytes.Length;
        return (bytes[index] & 1) == 0;
    }

    private static bool InFinder(int row, int col, int startRow, int startCol)
    {
        var localRow = row - startRow;
        var localCol = col - startCol;
        if (localRow < 0 || localRow > 6 || localCol < 0 || localCol > 6)
        {
            return false;
        }

        return localRow is 0 or 6 || localCol is 0 or 6 || (localRow is >= 2 and <= 4 && localCol is >= 2 and <= 4);
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character >= 32 && character <= 126)
            {
                buffer.Append(character);
                continue;
            }

            buffer.Append(character switch
            {
                'Ñ' => 'N',
                'ñ' => 'n',
                _ => ' '
            });
        }

        return buffer.ToString().Trim();
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private enum TextAlign
    {
        Left,
        Center,
        Right
    }
}
