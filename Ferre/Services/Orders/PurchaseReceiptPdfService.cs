using System.Globalization;
using System.Text;
using Ferre.Models.Orders;

namespace Ferre.Services.Orders;

public sealed class PurchaseReceiptPdfService : IPurchaseReceiptPdfService
{
    public byte[] Generate(ClientPurchaseReceipt receipt, string customerEmail, string customerName)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var lines = BuildLines(receipt, customerEmail, customerName);
        var contentStream = BuildContentStream(lines);
        return BuildSinglePagePdf(contentStream);
    }

    private static List<string> BuildLines(ClientPurchaseReceipt receipt, string customerEmail, string customerName)
    {
        var result = new List<string>
        {
            "NEXO FERRETERO - COMPROBANTE DIGITAL",
            $"Comprobante: {receipt.ReceiptNumber}",
            $"Fecha UTC: {receipt.CreatedAtUtc:yyyy-MM-dd HH:mm:ss}",
            $"Cliente: {(string.IsNullOrWhiteSpace(customerName) ? "Cliente" : customerName)}",
            $"Correo: {customerEmail}",
            $"Metodo de pago: {receipt.PaymentMethod}",
            $"Estado: {receipt.Status}",
            string.Empty,
            "Detalle"
        };

        foreach (var line in receipt.Lines)
        {
            result.Add($"- {line.ProductName} | Cant: {line.Quantity} | PU: {line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)} | Total: {line.LineTotal.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        result.Add(string.Empty);
        result.Add($"TOTAL: {receipt.Total.ToString("0.00", CultureInfo.InvariantCulture)} USD");
        result.Add("Documento generado automaticamente por la tienda.");

        return result;
    }

    private static string BuildContentStream(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BT");
        builder.AppendLine("/F1 11 Tf");
        builder.AppendLine("50 790 Td");

        var firstLine = true;
        foreach (var rawLine in lines)
        {
            var safeLine = EscapePdfText(NormalizeText(rawLine));
            if (!firstLine)
            {
                builder.AppendLine("0 -16 Td");
            }

            builder.AppendLine($"({safeLine}) Tj");
            firstLine = false;
        }

        builder.AppendLine("ET");
        return builder.ToString();
    }

    private static byte[] BuildSinglePagePdf(string contentStream)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
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
}
