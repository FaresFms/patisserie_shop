using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Cashier;

/// <summary>
/// Host-level orchestrator for manual cashier-raised reports. A cashier on the POS can
/// flag a product that is running low at their branch; this records a Pending
/// <c>StockReport</c> decision-log entry (carrying the sentinel rule id) so the manager
/// sees it on the Decision Log / bell. Informational only — there is no corrective
/// document to execute; the manager acknowledges or dismisses it.
/// </summary>
public interface ICashierReportAppService : IApplicationService
{
    Task ReportLowStockAsync(ReportLowStockInput input);
}
