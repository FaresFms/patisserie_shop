using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Analytics;

/// <summary>
/// Host-level read-model that composes Inventory WriteOff-movement aggregates
/// (valued at product CostPrice) with branch/product names and the Operations
/// sales totals (the waste-to-sales denominator). Branch scoping mirrors
/// <see cref="ISalesAnalyticsAppService"/>: users without StockMovements.ViewAll
/// only ever see their own branches.
/// </summary>
public interface IWasteAnalyticsAppService : IApplicationService
{
    Task<WasteAnalyticsDto> GetAsync(GetWasteAnalyticsInput input);
}
