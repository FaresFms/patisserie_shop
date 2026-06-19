using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Operations.PurchaseOrders;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Suppliers;

/// <summary>
/// Composes Operations PO delivery history with Inventory supplier records into
/// per-supplier scorecards — same cross-module, in-memory dictionary-overlay pattern
/// as <see cref="patisserie_shop.Analytics.SalesAnalyticsAppService"/>. All grouping
/// lives in <see cref="IPurchaseOrderRepository"/>; this service maps the read-model
/// onto DTOs, overlays the supplier name + configured lead time, and derives the
/// rates/grade with the entity-free <see cref="SupplierGrading"/> helper.
/// </summary>
[Authorize(InventoryPermissions.Suppliers.Default)]
public class SupplierScorecardAppService : patisserie_shopAppService, ISupplierScorecardAppService
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;

    public SupplierScorecardAppService(
        IPurchaseOrderRepository purchaseOrderRepository,
        IRepository<AppSupplier, Guid> supplierRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<List<SupplierScorecardDto>> GetListAsync(GetSupplierScorecardsInput input)
    {
        var days = NormalizeDays(input.Days);

        var toUtc = DateTime.UtcNow.Date.AddDays(1);
        var fromUtc = toUtc.AddDays(-days);

        // All suppliers carry a row even when they have no delivery history yet — the
        // page lists every supplier and merges scorecard metrics in by id, so an empty
        // (N/A) scorecard is the right default rather than a missing row.
        var suppliers = await _supplierRepository.GetListAsync();

        var scorecards = await _purchaseOrderRepository.GetSupplierScorecardsAsync(
            fromUtc, toUtc, supplierIds: null);
        var bySupplier = scorecards.ToDictionary(s => s.SupplierId);

        var result = new List<SupplierScorecardDto>(suppliers.Count);
        foreach (var supplier in suppliers)
        {
            bySupplier.TryGetValue(supplier.Id, out var row);
            result.Add(ToDto(supplier.Id, supplier.Name, supplier.LeadTimeDays, row));
        }

        return result;
    }

    private static SupplierScorecardDto ToDto(
        Guid supplierId,
        string supplierName,
        int configuredLeadTimeDays,
        SupplierScorecardRow? row)
    {
        if (row == null)
        {
            return new SupplierScorecardDto
            {
                SupplierId = supplierId,
                SupplierName = supplierName,
                ConfiguredLeadTimeDays = configuredLeadTimeDays,
                Grade = SupplierGrading.NotAvailable
            };
        }

        var ratedOrders = row.OnTimeOrders + row.LateOrders;
        double? onTimeRate = ratedOrders > 0
            ? Math.Round(row.OnTimeOrders * 100.0 / ratedOrders, 1)
            : null;

        double? fillRate = row.TotalOrderedQty > 0
            ? Math.Round(row.TotalReceivedQty * 100.0 / row.TotalOrderedQty, 1)
            : null;

        int? measuredLeadTime = row.AvgActualLeadTimeDays.HasValue
            ? (int)Math.Round(row.AvgActualLeadTimeDays.Value, MidpointRounding.AwayFromZero)
            : null;

        return new SupplierScorecardDto
        {
            SupplierId = supplierId,
            SupplierName = supplierName,
            ConfiguredLeadTimeDays = configuredLeadTimeDays,
            TotalOrders = row.TotalOrders,
            ReceivedOrders = row.ReceivedOrders,
            OnTimeOrders = row.OnTimeOrders,
            LateOrders = row.LateOrders,
            TotalOrderedQty = row.TotalOrderedQty,
            TotalReceivedQty = row.TotalReceivedQty,
            OnTimeRate = onTimeRate,
            FillRate = fillRate,
            AvgDelayDays = row.AvgDelayDays.HasValue
                ? Math.Round(row.AvgDelayDays.Value, 1)
                : null,
            MeasuredLeadTimeDays = measuredLeadTime,
            LeadTimeSampleSize = row.LeadTimeSampleSize,
            Grade = SupplierGrading.Grade(onTimeRate, fillRate)
        };
    }

    private static int NormalizeDays(int days)
        => days <= 30 ? 30 : days <= 90 ? 90 : 180;
}
