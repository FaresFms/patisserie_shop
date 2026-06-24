using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Planning;

/// <summary>
/// Host-level read-model that answers "what is going to happen this month?" by
/// composing three deterministic forward signals — predicted stockouts (velocity ×
/// current stock via ForecastWalker), batches expiring (the batch ledger) and
/// expected PO deliveries (open purchase orders). Branch scoping mirrors the
/// dashboards: users without BranchInventory.ManageAll only ever see their own
/// branches.
/// </summary>
public interface IReorderCalendarAppService : IApplicationService
{
    Task<ReorderCalendarDto> GetAsync(GetReorderCalendarInput input);
}
