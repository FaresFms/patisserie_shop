using System;
using System.Linq;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Operations.PurchaseOrders;

public class PurchaseOrderManager : DomainService
{
    private readonly IRepository<AppPurchaseOrder, Guid> _poRepository;

    public PurchaseOrderManager(IRepository<AppPurchaseOrder, Guid> poRepository)
    {
        _poRepository = poRepository;
    }

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
    /// Generates PO-YYYY-NNNN. Sequence resets per year, computed by scanning
    /// existing POs whose number starts with the year prefix. Race-safe enough for MVP;
    /// uniqueness is enforced by the DB unique index on PONumber.
    /// </summary>
    public async Task<string> GenerateNumberAsync(int year)
    {
        var prefix = $"PO-{year:D4}-";
        var queryable = await _poRepository.GetQueryableAsync();
        var lastNumber = await AsyncExecuter.MaxAsync(
            queryable.Where(p => p.PONumber.StartsWith(prefix)).Select(p => (string?)p.PONumber),
            x => x);

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var tail = lastNumber.Substring(prefix.Length);
            if (int.TryParse(tail, out var n))
            {
                nextSeq = n + 1;
            }
        }
        return $"{prefix}{nextSeq:D4}";
    }
}
