using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Decisions;

/// <summary>
/// Host-level orchestrator that closes the decision→action loop: executing a
/// decision log entry creates the corrective document as a DRAFT (a human still
/// approves it) and records the link on the decision log row.
/// </summary>
public interface IDecisionActionAppService : IApplicationService
{
    Task<DecisionActionResultDto> ExecuteDecisionAsync(Guid decisionLogId);

    /// <summary>
    /// Consolidates pending reorder-type decisions (LowStockAlert, ReorderSuggestion,
    /// StockoutRisk) into DRAFT purchase orders, one per (supplier × destination branch)
    /// group — purchasing batches orders rather than cutting a PO per SKU. Decisions
    /// whose product has no default supplier are skipped (and counted), never fatal.
    /// A human still approves the resulting drafts.
    /// </summary>
    Task<ConsolidationResultDto> ConsolidateReordersAsync(ConsolidateReordersInput input);
}
