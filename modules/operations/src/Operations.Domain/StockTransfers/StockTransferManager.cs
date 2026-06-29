using System;
using Operations.Entities;
using Volo.Abp.Domain.Services;

namespace Operations.StockTransfers;

public class StockTransferManager : DomainService
{
    /// <summary>
    /// Creates a Draft transfer. The same-branch invariant is enforced inside the
    /// aggregate constructor; this factory keeps aggregate creation in the domain
    /// (id generation, defaulting) so the app service stays a thin orchestrator.
    /// </summary>
    public AppStockTransfer CreateDraft(
        Guid? fromBranchId,
        Guid toBranchId,
        DateTime requestedDate,
        Guid? requestedByUserId,
        string? notes)
    {
        return new AppStockTransfer(
            GuidGenerator.Create(),
            fromBranchId,
            toBranchId,
            requestedDate,
            requestedByUserId,
            notes);
    }
}
