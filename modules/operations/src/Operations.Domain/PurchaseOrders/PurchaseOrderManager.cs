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

        // Take the max by the PARSED numeric tail, not a lexical string Max: once the
        // sequence passes 9999 the tail widens and "PO-2026-10000" sorts BEFORE
        // "PO-2026-9999" lexically, which would reissue a duplicate number. Pulling the
        // year's numbers and maxing the integer suffix is correct at any width; the
        // per-year, per-branch volume for a patisserie keeps this list tiny.
        var numbers = await AsyncExecuter.ToListAsync(
            queryable.Where(p => p.PONumber.StartsWith(prefix)).Select(p => p.PONumber));

        var maxSeq = 0;
        foreach (var number in numbers)
        {
            if (int.TryParse(number.Substring(prefix.Length), out var n) && n > maxSeq)
            {
                maxSeq = n;
            }
        }
        return $"{prefix}{maxSeq + 1:D4}";
    }
}
