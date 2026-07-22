using System;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Operations.PurchaseOrders;

public class PurchaseOrderManager : DomainService
{
    public async Task<AppPurchaseOrder> CreateDraftAsync(
        Guid supplierId,
        Guid destBranchId,
        DateTime orderDate,
        DateTime? expectedDeliveryDate,
        string currency,
        string? notes)
    {
        var poNumber = await GenerateNumberAsync(orderDate.Year);
        return new AppPurchaseOrder(
            GuidGenerator.Create(),
            supplierId,
            destBranchId,
            poNumber,
            orderDate,
            expectedDeliveryDate,
            string.IsNullOrWhiteSpace(currency) ? "USD" : currency.ToUpper(),
            notes);
    }

    /// <summary>
    /// Generates PO-YYYY-XXXXXXXX. The GUID suffix avoids duplicate numbers when
    /// multiple users create purchase orders at the same time.
    /// </summary>
    public Task<string> GenerateNumberAsync(int year)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return Task.FromResult($"PO-{year:D4}-{suffix}");
    }

    public void EnsureReceiptExpiryIsUsable(
        bool tracksExpiry,
        Guid productId,
        string productName,
        DateTime? expiryDate,
        DateTime today)
    {
        if (!tracksExpiry)
        {
            return;
        }

        if (!expiryDate.HasValue)
        {
            throw new BusinessException(OperationsErrorCodes.PurchaseExpiryRequired)
                .WithData("ProductId", productId)
                .WithData("ProductName", productName);
        }
        if (expiryDate.Value.Date < today.Date)
        {
            throw new BusinessException(OperationsErrorCodes.PurchaseExpiryInPast)
                .WithData("ProductId", productId)
                .WithData("ProductName", productName)
                .WithData("ExpiryDate", expiryDate.Value.Date.ToString("yyyy-MM-dd"));
        }
    }
}
