using System;
using System.Text.Json.Serialization;

namespace Operations.StockTransfers;

public class CreateStockTransferDto
{
    public Guid? FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    // These values are intentionally excluded from JSON input. They let another
    // in-process module correlate a transfer with its own document without exposing
    // a client-spoofable reference on the general stock-transfer HTTP endpoint.
    [JsonIgnore]
    public string? SourceDocumentType { get; private set; }

    [JsonIgnore]
    public Guid? SourceDocumentId { get; private set; }

    [JsonIgnore]
    public Guid? SourceDocumentItemId { get; private set; }

    public void SetSourceDocument(string sourceDocumentType, Guid sourceDocumentId, Guid? sourceDocumentItemId = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDocumentType))
        {
            throw new ArgumentException("Source document type is required.", nameof(sourceDocumentType));
        }

        SourceDocumentType = sourceDocumentType.Trim();
        SourceDocumentId = sourceDocumentId;
        SourceDocumentItemId = sourceDocumentItemId;
    }
}

public class AssignStockTransferSourceDto
{
    public Guid FromBranchId { get; set; }
}
