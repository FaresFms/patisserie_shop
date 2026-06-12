using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Analytics;

/// <summary>
/// Host-level read-model that composes Operations sales aggregates with
/// Inventory product/category names into a deep-dive analytics view the
/// dashboards don't provide. Branch scoping mirrors the dashboards: users
/// without Sales.ManageAll only ever see their own branches.
/// </summary>
public interface ISalesAnalyticsAppService : IApplicationService
{
    Task<SalesAnalyticsDto> GetAsync(GetSalesAnalyticsInput input);
}
