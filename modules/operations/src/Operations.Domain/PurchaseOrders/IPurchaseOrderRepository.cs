using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Operations.Entities;
using Volo.Abp.Domain.Repositories;

namespace Operations.PurchaseOrders;

/// <summary>
/// Custom repository for <see cref="AppPurchaseOrder"/>. Owns the supplier
/// delivery-performance aggregation that backs the host supplier scorecard and
/// the measured-lead-time feedback into the reorder-point formula. Heavy grouping
/// lives here so neither the app service nor a domain service ever touches a
/// queryable to compute it.
/// </summary>
public interface IPurchaseOrderRepository : IRepository<AppPurchaseOrder, Guid>
{
    /// <summary>
    /// Per-supplier delivery metrics over received/terminal POs whose OrderDate falls
    /// in [fromUtc, toUtcExclusive). Only Received and PartialReceived orders count.
    /// When <paramref name="supplierIds"/> is non-null only those suppliers are
    /// included (used by the single-supplier ROP lookup). Suppliers with no matching
    /// orders simply don't appear in the result.
    /// </summary>
    Task<List<SupplierScorecardRow>> GetSupplierScorecardsAsync(
        DateTime fromUtc,
        DateTime toUtcExclusive,
        IReadOnlyCollection<Guid>? supplierIds,
        CancellationToken cancellationToken = default);
}
